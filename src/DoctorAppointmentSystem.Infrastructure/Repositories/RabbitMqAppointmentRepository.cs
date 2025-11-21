using DoctorAppointmentSystem.Core.Entities;
using DoctorAppointmentSystem.Core.Interfaces;
using DoctorAppointmentSystem.Core.Exceptions;
using DoctorAppointmentSystem.Core.DTOs;

namespace DoctorAppointmentSystem.Infrastructure.Repositories;

/// <summary>
/// Appointment repository using Redis + RabbitMQ for eventual consistency
/// Provides sub-50ms response times by deferring DB writes to background workers
/// </summary>
public class RabbitMqAppointmentRepository : IAppointmentWriteRepository
{
    private readonly IRedisSerialNumberService _redisSerialService;
    private readonly IAppointmentMessagePublisher _messagePublisher;
    private readonly IAppointmentStatusTracker _statusTracker;

    public RabbitMqAppointmentRepository(
        IRedisSerialNumberService redisSerialService,
        IAppointmentMessagePublisher messagePublisher,
        IAppointmentStatusTracker statusTracker)
    {
        _redisSerialService = redisSerialService;
        _messagePublisher = messagePublisher;
        _statusTracker = statusTracker;
    }

    public async Task<object> CreateAppointmentAsync(
        DoctorHospital doctorHospital,
        int patientId,
        DateOnly appointmentDate,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        // 1. Get next serial number from Redis (atomic, fast)
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

        // 2. Generate appointment reference
        var appointmentRef = Guid.NewGuid().ToString("N");

        // 3. Publish to RabbitMQ for async processing
        var message = new AppointmentCreationMessage
        {
            AppointmentReference = appointmentRef,
            DoctorHospitalId = doctorHospital.Id,
            PatientId = patientId,
            AppointmentDate = appointmentDate,
            SerialNumber = serialNumber.Value,
            Notes = notes,
            QueuedAt = DateTime.UtcNow
        };

        try
        {
            await _messagePublisher.PublishAppointmentCreationAsync(message, cancellationToken);
            await _statusTracker.SetProcessingAsync(appointmentRef, cancellationToken);

            return appointmentRef; // Return reference for tracking
        }
        catch (Exception ex)
        {
            throw new AppointmentCreationFailedException(
                $"Failed to queue appointment: {ex.Message}");
        }
    }
}
