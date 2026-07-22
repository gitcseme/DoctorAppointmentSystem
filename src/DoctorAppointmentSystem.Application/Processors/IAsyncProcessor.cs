using DoctorAppointmentSystem.Core.Entities;

namespace DoctorAppointmentSystem.Application.Processors;

public interface IAsyncProcessor
{
    Task<AsyncAppointmentResult> ProcessAsync(
        DoctorHospital doctorHospital,
        int patientId,
        DateOnly appointmentDate,
        string? notes,
        CancellationToken cancellationToken = default);

    Task<AsyncAppointmentStatusResult?> GetStatusAsync(
        string appointmentReference,
        CancellationToken cancellationToken = default);
}

public record AsyncAppointmentResult(
    string AppointmentReference,
    string Message,
    string StatusUrl
);

public record AsyncAppointmentStatusResult(
    bool Success,
    int? AppointmentId,
    string? ErrorMessage,
    DateTime? ProcessedAt
);