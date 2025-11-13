using DoctorAppointmentSystem.Core.Entities;

namespace DoctorAppointmentSystem.Core.Interfaces;

public interface IPatientRepository
{
    Task<Patient> CreateAsync(Patient patient, CancellationToken cancellationToken = default);
    Task<Patient?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Patient>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int patientId, CancellationToken cancellationToken);
}
