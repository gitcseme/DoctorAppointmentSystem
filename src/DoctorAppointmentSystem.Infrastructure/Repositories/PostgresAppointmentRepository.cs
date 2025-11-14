using Microsoft.EntityFrameworkCore;
using DoctorAppointmentSystem.Core.Entities;
using DoctorAppointmentSystem.Core.Interfaces;
using DoctorAppointmentSystem.Core.Exceptions;
using DoctorAppointmentSystem.Infrastructure.Data;

namespace DoctorAppointmentSystem.Infrastructure.Repositories;

public class PostgresAppointmentRepository : IAppointmentRepository
{
    private readonly AppDbContext _context;

    public PostgresAppointmentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> CreateAppointmentAsync(
        DoctorHospital doctorHospital,
        int patientId,
        DateOnly appointmentDate,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        // Since we are using transactions manually, EF Core will not automatically retry on transient failures.
        // We need strategy for re-trying the entire operation in case of transient failures
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.ReadCommitted,
                cancellationToken);

            try
            {
                // 1. Using raw SQL with FOR UPDATE to acquire row-level lock
                var counter = await _context.AppointmentCounters
                    .FromSqlRaw(@"
                     SELECT * FROM appointment_counters 
                     WHERE doctor_hospital_id = {0} AND appointment_date = {1}
                     FOR UPDATE",
                        doctorHospital.Id,
                        appointmentDate)
                    .FirstOrDefaultAsync(cancellationToken);

                // 2. If counter doesn't exist, create it
                counter = await InsertCounterIfNotExistsAsync(doctorHospital, appointmentDate, counter, cancellationToken);

                // 3. Check if the daily limit has been reached
                if (counter.AppointmentCount >= doctorHospital.DailyPatientLimit)
                {
                    throw new DailyLimitReachedException(
                        $"Daily patient limit ({doctorHospital.DailyPatientLimit}) reached for this doctor at this hospital on {appointmentDate}.");
                }

                // 4. Increment the counter atomically
                counter.CurrentSerial++;
                counter.AppointmentCount++;
                counter.UpdatedAt = DateTime.UtcNow;

                // 5. Create the appointment with the new serial number
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
                await _context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return appointment.Id;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<AppointmentCounter> InsertCounterIfNotExistsAsync(DoctorHospital doctorHospital, 
        DateOnly appointmentDate, 
        AppointmentCounter? counter, 
        CancellationToken cancellationToken)
    {
        if (counter is not null)
        {
            return counter;
        }

        var newCounter = new AppointmentCounter
        {
            DoctorHospitalId = doctorHospital.Id,
            AppointmentDate = appointmentDate,
            CurrentSerial = 0,
            AppointmentCount = 0,
            UpdatedAt = DateTime.UtcNow
        };
        _context.AppointmentCounters.Add(newCounter);
        await _context.SaveChangesAsync(cancellationToken);

        return newCounter;
    }

    public async Task<IEnumerable<object>> GetAppointmentsByDoctorAndDateAsync(
        int doctorId,
        int hospitalId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        return await _context.Appointments
            .Include(a => a.DoctorHospital)
                .ThenInclude(dh => dh.Doctor)
            .Include(a => a.DoctorHospital)
                .ThenInclude(dh => dh.Hospital)
            .Include(a => a.Patient)
            .Where(a => a.DoctorHospital.DoctorId == doctorId
                     && a.DoctorHospital.HospitalId == hospitalId
                     && a.AppointmentDate == date)
            .OrderBy(a => a.SerialNumber)
            .Select(a => new
            {
                a.Id,
                a.SerialNumber,
                a.AppointmentDate,
                a.Status,
                a.Notes,
                Patient = new
                {
                    a.Patient.Id,
                    a.Patient.Name,
                    a.Patient.PhoneNumber
                },
                a.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<object?> GetAppointmentByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Appointments
            .Where(a => a.Id == id)
            .Select(a => new
            {
                a.Id,
                Doctor = new
                {
                    a.DoctorHospital.Doctor.Id,
                    a.DoctorHospital.Doctor.Name,
                    a.DoctorHospital.Doctor.Specialization
                },
                Hospital = new
                {
                    a.DoctorHospital.Hospital.Id,
                    a.DoctorHospital.Hospital.Name,
                    a.DoctorHospital.Hospital.City
                },
                Patient = new
                {
                    a.Patient.Id,
                    a.Patient.Name,
                    a.Patient.Email,
                    a.Patient.PhoneNumber
                },
                a.AppointmentDate,
                a.SerialNumber,
                a.Status,
                a.Notes,
                a.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> CheckAppointmentExistsAsync(int patientId, int doctorHospitalId, DateOnly appointmentDate, CancellationToken cancellationToken = default)
    {
        return _context.Appointments.AnyAsync(a =>
            a.PatientId == patientId &&
            a.DoctorHospitalId == doctorHospitalId &&
            a.AppointmentDate == appointmentDate,
            cancellationToken);
    }
}
