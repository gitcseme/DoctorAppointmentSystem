using Microsoft.EntityFrameworkCore;
using DoctorAppointmentSystem.Application.Processors;
using DoctorAppointmentSystem.Application.Services;
using DoctorAppointmentSystem.Core.Entities;
using DoctorAppointmentSystem.Core.Exceptions;
using DoctorAppointmentSystem.Core.Interfaces;
using DoctorAppointmentSystem.Infrastructure.Data;

namespace DoctorAppointmentSystem.Application.Services;

public class SyncAppointmentService : ISyncAppointmentService
{
    private readonly ISyncProcessor _processor;
    private readonly IDoctorRepository _doctorRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly AppDbContext _context;

    public SyncAppointmentService(
        ISyncProcessor processor,
        IDoctorRepository doctorRepository,
        IPatientRepository patientRepository,
        AppDbContext context)
    {
        _processor = processor;
        _doctorRepository = doctorRepository;
        _patientRepository = patientRepository;
        _context = context;
    }

    public async Task<SyncAppointmentResponse> CreateAsync(
        int doctorId,
        int hospitalId,
        int patientId,
        DateOnly appointmentDate,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var patient = await _patientRepository.GetByIdAsync(patientId, cancellationToken);
        if (patient == null)
        {
            throw new EntityNotFoundException($"Patient with ID {patientId} not found.");
        }

        var doctorHospital = await _doctorRepository.GetDoctorHospitalAsync(
            doctorId, hospitalId, cancellationToken);

        if (doctorHospital == null)
        {
            throw new EntityNotFoundException(
                $"Doctor with ID {doctorId} is not associated with hospital ID {hospitalId}.");
        }

        var exists = await _context.Appointments.AnyAsync(
            a => a.PatientId == patientId
                && a.DoctorHospitalId == doctorHospital.Id
                && a.AppointmentDate == appointmentDate,
            cancellationToken);

        if (exists)
        {
            throw new DuplicateAppointmentException(
                $"Appointment already exists for this patient with this doctor on {appointmentDate}.");
        }

        var result = await _processor.ProcessAsync(
            doctorHospital, patientId, appointmentDate, notes, cancellationToken);

        return new SyncAppointmentResponse(
            result.AppointmentId,
            result.SerialNumber,
            result.Message);
    }

    public async Task<object?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Appointments
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new
            {
                a.Id,
                Doctor = new
                {
                    a.DoctorHospital.Doctor.Id,
                    a.DoctorHospital.Doctor.Name,
                    a.DoctorHospital.Doctor.Specialization
                },
                Hospital = new
                {
                    a.DoctorHospital.Hospital.Id,
                    a.DoctorHospital.Hospital.Name,
                    a.DoctorHospital.Hospital.City
                },
                Patient = new
                {
                    a.Patient.Id,
                    a.Patient.Name,
                    a.Patient.Email,
                    a.Patient.PhoneNumber
                },
                a.AppointmentDate,
                a.SerialNumber,
                a.Status,
                a.Notes,
                a.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<object>> GetByDoctorAndDateAsync(
        int doctorId,
        int hospitalId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        return await _context.Appointments
            .AsNoTracking()
            .Include(a => a.DoctorHospital)
                .ThenInclude(dh => dh.Doctor)
            .Include(a => a.DoctorHospital)
                .ThenInclude(dh => dh.Hospital)
            .Include(a => a.Patient)
            .Where(a => a.DoctorHospital.DoctorId == doctorId
                     && a.DoctorHospital.HospitalId == hospitalId
                     && a.AppointmentDate == date)
            .OrderBy(a => a.SerialNumber)
            .Select(a => new
            {
                a.Id,
                a.SerialNumber,
                a.AppointmentDate,
                a.Status,
                a.Notes,
                Patient = new
                {
                    a.Patient.Id,
                    a.Patient.Name,
                    a.Patient.PhoneNumber
                },
                a.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}