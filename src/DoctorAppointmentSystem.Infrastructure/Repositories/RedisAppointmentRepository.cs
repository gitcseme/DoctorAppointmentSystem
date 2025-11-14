using Microsoft.EntityFrameworkCore;
using DoctorAppointmentSystem.Core.Entities;
using DoctorAppointmentSystem.Core.Interfaces;
using DoctorAppointmentSystem.Core.Exceptions;
using DoctorAppointmentSystem.Infrastructure.Data;

namespace DoctorAppointmentSystem.Infrastructure.Repositories;

/// <summary>
/// Appointment repository using Redis distributed locking for atomic serial number assignment
/// No PostgreSQL row-level locks - all concurrency handled by Redis across multiple API instances
/// </summary>
public class RedisAppointmentRepository : IAppointmentRepository
{
    private readonly AppDbContext _context;
    private readonly IRedisSerialNumberService _redisSerialService;

    public RedisAppointmentRepository(
        AppDbContext context,
        IRedisSerialNumberService redisSerialService)
    {
        _context = context;
        _redisSerialService = redisSerialService;
    }

    public async Task<int> CreateAppointmentAsync(
        DoctorHospital doctorHospital,
        int patientId,
        DateOnly appointmentDate,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var serialNumber = await _redisSerialService.GetNextSerialNumberAsync(
            doctorHospital.Id,
            appointmentDate,
            doctorHospital.DailyPatientLimit,
            cancellationToken);

        if (serialNumber is null)
        {
            throw new DailyLimitReachedException(
                $"Daily patient limit ({doctorHospital.DailyPatientLimit}) reached for this doctor at this hospital on {appointmentDate}.");
        }

        try
        {
            // 3. Create the appointment with the assigned serial number (no DB lock needed)
            var appointment = new Appointment
            {
                DoctorHospitalId = doctorHospital.Id,
                PatientId = patientId,
                AppointmentDate = appointmentDate,
                SerialNumber = serialNumber.Value,
                Status = AppointmentStatus.Scheduled,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync(cancellationToken);

            return appointment.Id;
        }
        catch (Exception ex)
        {
            throw new AppointmentCreationFailedException($"Failed to create appointment: {ex.Message}");
        }
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
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<object?> GetAppointmentByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Appointments
            .AsNoTracking()
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

    public Task<bool> CheckAppointmentExistsAsync(
        int patientId, 
        int doctorHospitalId, 
        DateOnly appointmentDate, 
        CancellationToken cancellationToken = default)
    {
        return _context.Appointments
            .AnyAsync(a =>
                a.PatientId == patientId &&
                a.DoctorHospitalId == doctorHospitalId &&
                a.AppointmentDate == appointmentDate,
                cancellationToken);
    }
}
