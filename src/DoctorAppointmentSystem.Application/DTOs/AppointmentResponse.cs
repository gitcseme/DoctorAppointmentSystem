using DoctorAppointmentSystem.Core.Entities;

namespace DoctorAppointmentSystem.Application.DTOs;

public record AppointmentResult(
    bool Success,
    int? AppointmentId,
    string? AppointmentReference,
    AppointmentStatus Status,
    string? Message,
    string? ErrorMessage
)
{
    public static AppointmentResult Created(int appointmentId, int serialNumber) => new(
        Success: true,
        AppointmentId: appointmentId,
        AppointmentReference: null,
        Status: AppointmentStatus.Scheduled,
        Message: $"Appointment created successfully with serial number {serialNumber}",
        ErrorMessage: null
    );

    public static AppointmentResult Processing(string appointmentReference) => new(
        Success: true,
        AppointmentId: null,
        AppointmentReference: appointmentReference,
        Status: AppointmentStatus.Scheduled,
        Message: "Appointment is being processed. Use reference to check status.",
        ErrorMessage: null
    );

    public static AppointmentResult Failed(string errorMessage) => new(
        Success: false,
        AppointmentId: null,
        AppointmentReference: null,
        Status: AppointmentStatus.Cancelled,
        Message: null,
        ErrorMessage: errorMessage
    );
}

public record AppointmentStatusDetail(
    bool Success,
    int? AppointmentId,
    string? AppointmentReference,
    AppointmentStatus Status,
    string? Message,
    string? ErrorMessage,
    DateTime? ProcessedAt
);