using DoctorAppointmentSystem.Core.Entities;

namespace DoctorAppointmentSystem.Core.Interfaces;

public interface IDoctorRepository
{
    Task<Doctor> CreateAsync(Doctor doctor, CancellationToken cancellationToken = default);
    Task<Doctor?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Doctor>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<DoctorHospital?> GetDoctorHospitalAsync(int doctorId, int hospitalId, CancellationToken cancellationToken = default);
    Task AssignToHospitalAsync(int doctorId, int hospitalId, int dailyPatientLimit, CancellationToken cancellationToken = default);
}
