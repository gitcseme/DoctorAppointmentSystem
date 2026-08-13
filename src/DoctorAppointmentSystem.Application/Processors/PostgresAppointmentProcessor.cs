using Microsoft.EntityFrameworkCore;
using DoctorAppointmentSystem.Application.Processors;
using DoctorAppointmentSystem.Core.Entities;
using DoctorAppointmentSystem.Core.Exceptions;
using DoctorAppointmentSystem.Infrastructure.Data;

namespace DoctorAppointmentSystem.Application.Processors;

public class PostgresAppointmentProcessor : ISyncProcessor
{
    private readonly AppDbContext _context;

    public PostgresAppointmentProcessor(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SyncAppointmentResult> ProcessAsync(
        DoctorHospital doctorHospital,
        int patientId,
        DateOnly appointmentDate,
        string? notes,
        CancellationToken ctn = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.ReadCommitted,
                ctn);

            try
            {
                var counter = await InsertCounterIfNotExistsAsync(doctorHospital, appointmentDate, ctn);

                counter = await _context.AppointmentCounters
                    .FromSqlRaw(@"
                        SELECT * FROM appointment_counters 
                        WHERE doctor_hospital_id = {0} AND appointment_date = {1}
                        FOR UPDATE",
                        doctorHospital.Id,
                        appointmentDate)
                    .FirstOrDefaultAsync(ctn);

                if (counter.AppointmentCount >= doctorHospital.DailyPatientLimit)
                {
                    throw new DailyLimitReachedException(
                        $"Daily patient limit ({doctorHospital.DailyPatientLimit}) reached for this doctor at this hospital on {appointmentDate}.");
                }

                counter.CurrentSerial++;
                counter.AppointmentCount++;
                counter.UpdatedAt = DateTime.UtcNow;

                var appointment = new Appointment
                {
                    DoctorHospitalId = doctorHospital.Id,
                    PatientId = patientId,
                    AppointmentDate = appointmentDate,
                    SerialNumber = counter.CurrentSerial,
                    Status = AppointmentStatus.Scheduled,
                    Notes = notes,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync(ctn);

                await transaction.CommitAsync(ctn);

                return new SyncAppointmentResult(
                    appointment.Id,
                    appointment.SerialNumber,
                    $"Appointment created successfully with serial number {appointment.SerialNumber}");
            }
            catch
            {
                await transaction.RollbackAsync(ctn);
                throw;
            }
        });
    }

    private async Task<AppointmentCounter> InsertCounterIfNotExistsAsync(
        DoctorHospital doctorHospital,
        DateOnly appointmentDate,
        CancellationToken ctn)
    {
        var counter = await _context.AppointmentCounters
            .AsNoTracking()
            .Where(c => c.DoctorHospitalId == doctorHospital.Id && c.AppointmentDate == appointmentDate)
            .FirstOrDefaultAsync(ctn);

        if (counter is not null)
            return counter;

        // Insert new counter record for the doctor-hospital-date combination
        var newCounter = new AppointmentCounter
        {
            DoctorHospitalId = doctorHospital.Id,
            AppointmentDate = appointmentDate,
            CurrentSerial = 0,
            AppointmentCount = 0,
            UpdatedAt = DateTime.UtcNow
        };

        _context.AppointmentCounters.Add(newCounter);
        await _context.SaveChangesAsync(ctn);

        return newCounter;
    }
}