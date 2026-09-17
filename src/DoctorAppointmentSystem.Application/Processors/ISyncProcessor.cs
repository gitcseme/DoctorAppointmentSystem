using DoctorAppointmentSystem.Core.Entities;

namespace DoctorAppointmentSystem.Application.Processors;

public interface ISyncProcessor
{
    Task<SyncAppointmentResult> ProcessAsync(
        DoctorHospital doctorHospital,
        int patientId,
        DateOnly appointmentDate,
        string? notes,
        CancellationToken cancellationToken = default);
}

public record SyncAppointmentResult(
    int AppointmentId,
    int SerialNumber,
    string Message
);