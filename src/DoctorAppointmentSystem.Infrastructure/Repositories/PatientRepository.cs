using Microsoft.EntityFrameworkCore;
using DoctorAppointmentSystem.Core.Entities;
using DoctorAppointmentSystem.Core.Interfaces;
using DoctorAppointmentSystem.Infrastructure.Data;

namespace DoctorAppointmentSystem.Infrastructure.Repositories;

public class PatientRepository : IPatientRepository
{
    private readonly AppDbContext _context;

    public PatientRepository(AppDbContext context)
  {
        _context = context;
    }

    public async Task<Patient> CreateAsync(Patient patient, CancellationToken cancellationToken = default)
    {
  patient.CreatedAt = DateTime.UtcNow;
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync(cancellationToken);
        return patient;
    }

    public async Task<Patient?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Patients
 .Include(p => p.Appointments)
            .ThenInclude(a => a.DoctorHospital)
            .ThenInclude(dh => dh.Doctor)
     .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Patient>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Patients
            .ToListAsync(cancellationToken);
    }
}
