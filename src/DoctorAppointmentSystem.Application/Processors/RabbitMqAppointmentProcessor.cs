using DoctorAppointmentSystem.Application.Processors;
using DoctorAppointmentSystem.Core.Entities;
using DoctorAppointmentSystem.Core.Exceptions;
using DoctorAppointmentSystem.Core.DTOs;
using DoctorAppointmentSystem.Core.Interfaces;

namespace DoctorAppointmentSystem.Application.Processors;

public class RabbitMqAppointmentProcessor : IAsyncProcessor
{
    private readonly IRedisSerialNumberService _redisSerialService;
    private readonly IAppointmentMessagePublisher _messagePublisher;
    private readonly IAppointmentStatusTracker _statusTracker;

    public RabbitMqAppointmentProcessor(
        IRedisSerialNumberService redisSerialService,
        IAppointmentMessagePublisher messagePublisher,
        IAppointmentStatusTracker statusTracker)
    {
        _redisSerialService = redisSerialService;
        _messagePublisher = messagePublisher;
        _statusTracker = statusTracker;
    }

    public async Task<AsyncAppointmentResult> ProcessAsync(
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

        var appointmentRef = Guid.NewGuid().ToString("N");

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

            return new AsyncAppointmentResult(
                appointmentRef,
                "Appointment is being processed. Use reference to check status.",
                $"/api/v2/appointments/status/{appointmentRef}");
        }
        catch (Exception ex)
        {
            throw new AppointmentCreationFailedException(
                $"Failed to queue appointment: {ex.Message}");
        }
    }

    public async Task<AsyncAppointmentStatusResult?> GetStatusAsync(
        string appointmentReference,
        CancellationToken cancellationToken = default)
    {
        var status = await _statusTracker.GetStatusAsync(appointmentReference, cancellationToken);

        if (status == null)
            return null;

        return new AsyncAppointmentStatusResult(
            status.Success,
            status.AppointmentId,
            status.ErrorMessage,
            status.ProcessedAt);
    }
}