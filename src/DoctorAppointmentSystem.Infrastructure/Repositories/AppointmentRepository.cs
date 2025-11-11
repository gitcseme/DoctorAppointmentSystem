using Microsoft.EntityFrameworkCore;
using DoctorAppointmentSystem.Core.Entities;
using DoctorAppointmentSystem.Core.Interfaces;
using DoctorAppointmentSystem.Core.Exceptions;
using DoctorAppointmentSystem.Infrastructure.Data;

namespace DoctorAppointmentSystem.Infrastructure.Repositories;

/// <summary>
/// Repository that handles appointment creation with PostgreSQL row-level locking
/// to ensure atomic serial number assignment and prevent race conditions
/// </summary>
public class AppointmentRepository : IAppointmentRepository
{
    private readonly AppDbContext _context;

    public AppointmentRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Creates an appointment with atomic serial number assignment using AppointmentCounter table
    /// This prevents race conditions by locking only a single counter row per doctor-hospital-date
    /// </summary>
    public async Task<int> CreateAppointmentAsync(
        int doctorHospitalId,
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
                // 1. Lock and get (or create) the counter row for this doctor-hospital-date combination
                // Using raw SQL with FOR UPDATE to acquire row-level lock
                var counter = await _context.AppointmentCounters
                    .FromSqlRaw(@"
                     SELECT * FROM appointment_counters 
                     WHERE doctor_hospital_id = {0} AND appointment_date = {1}
                     FOR UPDATE",
                        doctorHospitalId,
                        appointmentDate)
                    .FirstOrDefaultAsync(cancellationToken);

                // 2. Get the doctor-hospital association to check the daily limit
                var doctorHospital = await _context.DoctorHospitals
                    .Where(dh => dh.Id == doctorHospitalId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (doctorHospital is null)
                {
                    throw new DoctorHospitalNotFoundException(
                        $"Doctor-Hospital association with ID {doctorHospitalId} not found.");
                }

                // 3. If counter doesn't exist, create it
                if (counter is null)
                {
                    counter = new AppointmentCounter
                    {
                        DoctorHospitalId = doctorHospitalId,
                        AppointmentDate = appointmentDate,
                        CurrentSerial = 0,
                        AppointmentCount = 0,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.AppointmentCounters.Add(counter);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                // 4. Check if the daily limit has been reached
                if (counter.AppointmentCount >= doctorHospital.DailyPatientLimit)
                {
                    throw new DailyLimitReachedException(
                        $"Daily patient limit ({doctorHospital.DailyPatientLimit}) reached for this doctor at this hospital on {appointmentDate}.");
                }

                // 5. Increment the counter atomically
                counter.CurrentSerial++;
                counter.AppointmentCount++;
                counter.UpdatedAt = DateTime.UtcNow;

                // 6. Create the appointment with the new serial number
                var appointment = new Appointment
                {
                    DoctorHospitalId = doctorHospitalId,
                    PatientId = patientId,
                    AppointmentDate = appointmentDate,
                    SerialNumber = counter.CurrentSerial,
                    Status = AppointmentStatus.Scheduled,
                    Notes = notes,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync(cancellationToken);

                // 7. Commit the transaction
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
