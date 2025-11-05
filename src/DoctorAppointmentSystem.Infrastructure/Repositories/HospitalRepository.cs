using Microsoft.EntityFrameworkCore;
using DoctorAppointmentSystem.Core.Entities;
using DoctorAppointmentSystem.Core.Interfaces;
using DoctorAppointmentSystem.Infrastructure.Data;

namespace DoctorAppointmentSystem.Infrastructure.Repositories;

public class HospitalRepository : IHospitalRepository
{
    private readonly AppDbContext _context;

    public HospitalRepository(AppDbContext context)
  {
        _context = context;
}

    public async Task<Hospital> CreateAsync(Hospital hospital, CancellationToken cancellationToken = default)
    {
  hospital.CreatedAt = DateTime.UtcNow;
   _context.Hospitals.Add(hospital);
        await _context.SaveChangesAsync(cancellationToken);
        return hospital;
    }

    public async Task<Hospital?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
     return await _context.Hospitals
    .Include(h => h.DoctorHospitals)
            .ThenInclude(dh => dh.Doctor)
 .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Hospital>> GetAllAsync(CancellationToken cancellationToken = default)
    {
   return await _context.Hospitals
         .Include(h => h.DoctorHospitals)
        .ThenInclude(dh => dh.Doctor)
      .ToListAsync(cancellationToken);
 }
}
