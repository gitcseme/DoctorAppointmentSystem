namespace DoctorAppointmentSystem.Application.Services;

public interface IAsyncAppointmentService
{
    Task<AsyncAppointmentResponse> CreateAsync(
        int doctorId,
        int hospitalId,
        int patientId,
        DateOnly appointmentDate,
        string? notes,
        CancellationToken cancellationToken = default);

    Task<AsyncAppointmentStatusResponse?> GetStatusAsync(
        string appointmentReference,
        CancellationToken cancellationToken = default);

    Task<object?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IEnumerable<object>> GetByDoctorAndDateAsync(
        int doctorId,
        int hospitalId,
        DateOnly date,
        CancellationToken cancellationToken = default);
}

public record AsyncAppointmentResponse(
    string AppointmentReference,
    string Message,
    string StatusUrl
);

public record AsyncAppointmentStatusResponse(
    bool Success,
    int? AppointmentId,
    string? ErrorMessage,
    DateTime? ProcessedAt
);