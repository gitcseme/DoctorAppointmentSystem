namespace DoctorAppointmentSystem.Core.Interfaces;

public interface IAppointmentRepository
{
    Task<int> CreateAppointmentAsync(int doctorHospitalId, int patientId, DateOnly appointmentDate, string? notes, CancellationToken cancellationToken = default);
    Task<IEnumerable<object>> GetAppointmentsByDoctorAndDateAsync(int doctorId, int hospitalId, DateOnly date, CancellationToken cancellationToken = default);
    Task<object?> GetAppointmentByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> CheckAppointmentExistsAsync(int patientId, int doctorHospitalId, DateOnly appointmentDate, CancellationToken cancellationToken = default);
}
