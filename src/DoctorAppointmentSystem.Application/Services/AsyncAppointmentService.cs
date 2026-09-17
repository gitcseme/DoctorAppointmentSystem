using Microsoft.EntityFrameworkCore;
using DoctorAppointmentSystem.Application.Processors;
using DoctorAppointmentSystem.Application.Services;
using DoctorAppointmentSystem.Core.Entities;
using DoctorAppointmentSystem.Core.Exceptions;
using DoctorAppointmentSystem.Core.Interfaces;
using DoctorAppointmentSystem.Infrastructure.Data;

namespace DoctorAppointmentSystem.Application.Services;

public class AsyncAppointmentService : IAsyncAppointmentService
{
    private readonly IAsyncProcessor _processor;
    private readonly IDoctorRepository _doctorRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly IAppointmentStatusTracker _statusTracker;
    private readonly AppDbContext _context;

    public AsyncAppointmentService(
        IAsyncProcessor processor,
        IDoctorRepository doctorRepository,
        IPatientRepository patientRepository,
        IAppointmentStatusTracker statusTracker,
        AppDbContext context)
    {
        _processor = processor;
        _doctorRepository = doctorRepository;
        _patientRepository = patientRepository;
        _statusTracker = statusTracker;
        _context = context;
    }

    public async Task<AsyncAppointmentResponse> CreateAsync(
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
                $"Doctor with ID {doctorId} is not associated with Hospital ID {hospitalId}.");
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

        var inFlightRef = Guid.NewGuid().ToString("N");
        var marked = await _statusTracker.MarkAsInFlightAsync(
            patientId, doctorHospital.Id, appointmentDate, inFlightRef, cancellationToken);

        if (!marked)
        {
            throw new DuplicateAppointmentException(
                $"An appointment request for this patient with this doctor on {appointmentDate} is already being processed.");
        }

        var result = await _processor.ProcessAsync(
            doctorHospital, patientId, appointmentDate, notes, cancellationToken);

        return new AsyncAppointmentResponse(
            result.AppointmentReference,
            result.Message,
            result.StatusUrl);
    }

    public async Task<AsyncAppointmentStatusResponse?> GetStatusAsync(
        string appointmentReference,
        CancellationToken cancellationToken = default)
    {
        var status = await _processor.GetStatusAsync(appointmentReference, cancellationToken);

        if (status == null)
            return null;

        return new AsyncAppointmentStatusResponse(
            status.Success,
            status.AppointmentId,
            status.ErrorMessage,
            status.ProcessedAt);
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