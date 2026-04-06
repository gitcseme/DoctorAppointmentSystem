using DoctorAppointmentSystem.Core.Entities;

namespace DoctorAppointmentSystem.Core.Interfaces;

/// <summary>
/// Repository for appointment write operations (commands)
/// </summary>
public interface IAppointmentWriteRepository
{
    /// <summary>
    /// Creates an appointment and returns an appointment reference for tracking
    /// </summary>
    Task<object> CreateAppointmentAsync(
        DoctorHospital doctorHospital,
        int patientId,
        DateOnly appointmentDate,
        string? notes,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository for appointment read operations (queries)
/// </summary>
public interface IAppointmentReadRepository
{
    Task<IEnumerable<object>> GetAppointmentsByDoctorAndDateAsync(
        int doctorId,
        int hospitalId,
        DateOnly date,
        CancellationToken cancellationToken = default);

    Task<object?> GetAppointmentByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<bool> CheckAppointmentExistsAsync(
        int patientId,
        int doctorHospitalId,
        DateOnly appointmentDate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Combined repository interface for backwards compatibility
/// </summary>
public interface IAppointmentRepository : IAppointmentWriteRepository, IAppointmentReadRepository
{
}
