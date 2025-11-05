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
    /// Creates an appointment with atomic serial number assignment using PostgreSQL FOR UPDATE lock
    /// This prevents race conditions when multiple requests come in simultaneously
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
                // 1. Lock the DoctorHospital row to prevent concurrent modifications
                // Use FromSqlRaw with FOR UPDATE to acquire a row-level lock
                var doctorHospital = await _context.DoctorHospitals
                    .FromSqlRaw(@"
                     SELECT * FROM doctor_hospitals 
                     WHERE id = {0} 
                     FOR UPDATE", doctorHospitalId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (doctorHospital is null)
                {
                    throw new DoctorHospitalNotFoundException(
                        $"Doctor-Hospital association with ID {doctorHospitalId} not found.");
                }

                // 2. Get the current count of appointments for this doctor-hospital-date combination
                // Lock these rows as well to prevent concurrent bookings
                var existingAppointmentsCount = await _context.Appointments
                    .FromSqlRaw(@"
                     SELECT * FROM appointments 
                     WHERE doctor_hospital_id = {0} 
                        AND appointment_date = {1} AND status != {2}
                     FOR UPDATE",
                        doctorHospitalId,
                        appointmentDate,
                        (int)AppointmentStatus.Cancelled)
                    .CountAsync(cancellationToken);

                // 3. Check if the daily limit has been reached
                if (existingAppointmentsCount >= doctorHospital.DailyPatientLimit)
                {
                    throw new DailyLimitReachedException(
                        $"Daily patient limit ({doctorHospital.DailyPatientLimit}) reached for this doctor at this hospital on {appointmentDate}.");
                }

                // 4. Get the next serial number (max + 1, or 1 if no appointments exist)
                var maxSerial = await _context.Appointments
                    .Where(a => a.DoctorHospitalId == doctorHospitalId
                        && a.AppointmentDate == appointmentDate)
                    .MaxAsync(a => (int?)a.SerialNumber, cancellationToken);

                var nextSerial = (maxSerial ?? 0) + 1;

                // 5. Create the appointment
                var appointment = new Appointment
                {
                    DoctorHospitalId = doctorHospitalId,
                    PatientId = patientId,
                    AppointmentDate = appointmentDate,
                    SerialNumber = nextSerial,
                    Status = AppointmentStatus.Scheduled,
                    Notes = notes,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync(cancellationToken);

                // 6. Commit the transaction
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
            .Include(a => a.DoctorHospital)
                .ThenInclude(dh => dh.Doctor)
            .Include(a => a.DoctorHospital)
                .ThenInclude(dh => dh.Hospital)
            .Include(a => a.Patient)
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
}
