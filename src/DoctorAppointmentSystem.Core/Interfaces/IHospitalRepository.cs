using DoctorAppointmentSystem.Core.Entities;

namespace DoctorAppointmentSystem.Core.Interfaces;

public interface IHospitalRepository
{
    Task<Hospital> CreateAsync(Hospital hospital, CancellationToken cancellationToken = default);
    Task<Hospital?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Hospital>> GetAllAsync(CancellationToken cancellationToken = default);
}
