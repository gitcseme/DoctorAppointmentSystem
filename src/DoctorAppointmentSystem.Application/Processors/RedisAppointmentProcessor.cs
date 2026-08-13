using Microsoft.EntityFrameworkCore;
using DoctorAppointmentSystem.Application.Processors;
using DoctorAppointmentSystem.Core.Entities;
using DoctorAppointmentSystem.Core.Exceptions;
using DoctorAppointmentSystem.Core.Interfaces;
using DoctorAppointmentSystem.Infrastructure.Data;

namespace DoctorAppointmentSystem.Application.Processors;

public class RedisAppointmentProcessor : ISyncProcessor
{
    private readonly AppDbContext _context;
    private readonly IRedisSerialNumberService _redisSerialService;

    public RedisAppointmentProcessor(AppDbContext context, IRedisSerialNumberService redisSerialService)
    {
        _context = context;
        _redisSerialService = redisSerialService;
    }

    public async Task<SyncAppointmentResult> ProcessAsync(
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

            return new SyncAppointmentResult(
                appointment.Id,
                appointment.SerialNumber,
                $"Appointment created successfully with serial number {appointment.SerialNumber}");
        }
        catch (Exception ex)
        {
            await _redisSerialService.DecrementSerialNumberAsync(
                doctorHospital.Id,
                appointmentDate,
                cancellationToken);

            throw new AppointmentCreationFailedException($"Failed to create appointment: {ex.Message}");
        }
    }
}