using DoctorAppointmentSystem.Core.Entities;

namespace DoctorAppointmentSystem.Core.Interfaces;

public interface IAppointmentRepository
{
    Task<int> CreateAppointmentAsync(DoctorHospital doctorHospital, int patientId, DateOnly appointmentDate, string? notes, CancellationToken cancellationToken = default);
    Task<IEnumerable<object>> GetAppointmentsByDoctorAndDateAsync(int doctorId, int hospitalId, DateOnly date, CancellationToken cancellationToken = default);
    Task<object?> GetAppointmentByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> CheckAppointmentExistsAsync(int patientId, int doctorHospitalId, DateOnly appointmentDate, CancellationToken cancellationToken = default);
}
