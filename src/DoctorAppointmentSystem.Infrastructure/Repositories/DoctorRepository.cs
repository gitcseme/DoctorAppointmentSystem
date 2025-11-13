using DoctorAppointmentSystem.Core.Entities;
using DoctorAppointmentSystem.Core.Interfaces;
using DoctorAppointmentSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DoctorAppointmentSystem.Infrastructure.Repositories;

public class DoctorRepository : IDoctorRepository
{
    private readonly AppDbContext _context;

    public DoctorRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Doctor> CreateAsync(Doctor doctor, CancellationToken cancellationToken = default)
    {
        doctor.CreatedAt = DateTime.UtcNow;
        _context.Doctors.Add(doctor);
        await _context.SaveChangesAsync(cancellationToken);
        return doctor;
    }

    public async Task<Doctor?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Doctors
            .Include(d => d.DoctorHospitals)
            .ThenInclude(dh => dh.Hospital)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Doctor>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Doctors
         .Include(d => d.DoctorHospitals)
        .ThenInclude(dh => dh.Hospital)
                .ToListAsync(cancellationToken);
    }

    public async Task<DoctorHospital?> GetDoctorHospitalAsync(int doctorId, int hospitalId, CancellationToken cancellationToken = default)
    {
        return await _context.DoctorHospitals
            .AsNoTracking()
            .FirstOrDefaultAsync(dh => dh.DoctorId == doctorId && dh.HospitalId == hospitalId, cancellationToken);
    }

    public async Task AssignToHospitalAsync(int doctorId, int hospitalId, int dailyPatientLimit, CancellationToken cancellationToken = default)
    {
        var doctorHospital = new DoctorHospital
        {
            DoctorId = doctorId,
            HospitalId = hospitalId,
            DailyPatientLimit = dailyPatientLimit,
            CreatedAt = DateTime.UtcNow
        };

        _context.DoctorHospitals.Add(doctorHospital);
        await _context.SaveChangesAsync(cancellationToken);
    }


}
