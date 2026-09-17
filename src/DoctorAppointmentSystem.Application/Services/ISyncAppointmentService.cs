namespace DoctorAppointmentSystem.Application.Services;

public interface ISyncAppointmentService
{
    Task<SyncAppointmentResponse> CreateAsync(
        int doctorId,
        int hospitalId,
        int patientId,
        DateOnly appointmentDate,
        string? notes,
        CancellationToken cancellationToken = default);

    Task<object?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IEnumerable<object>> GetByDoctorAndDateAsync(
        int doctorId,
        int hospitalId,
        DateOnly date,
        CancellationToken cancellationToken = default);
}

public record SyncAppointmentResponse(
    int AppointmentId,
    int SerialNumber,
    string Message
);