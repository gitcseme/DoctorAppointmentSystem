using Microsoft.AspNetCore.Mvc;
using DoctorAppointmentSystem.Core.DTOs;
using DoctorAppointmentSystem.Core.Interfaces;
using DoctorAppointmentSystem.Core.Exceptions;

namespace DoctorAppointmentSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IDoctorRepository _doctorRepository;
    private readonly IPatientRepository _patientRepository;

    public AppointmentsController(
        IAppointmentRepository appointmentRepository,
        IDoctorRepository doctorRepository,
        IPatientRepository patientRepository)
    {
        _appointmentRepository = appointmentRepository;
        _doctorRepository = doctorRepository;
        _patientRepository = patientRepository;
    }

    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> CreateAppointment([FromBody] CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var patient = await _patientRepository.GetByIdAsync(request.PatientId, cancellationToken);
            if (patient is null)
            {
                return NotFound(new { message = $"Patient with ID {request.PatientId} not found." });
            }

            var doctorHospital = await _doctorRepository.GetDoctorHospitalAsync(request.DoctorId, request.HospitalId, cancellationToken);
            if (doctorHospital == null)
            {
                return NotFound(new { message = $"Doctor with ID {request.DoctorId} is not associated with Hospital ID {request.HospitalId}." });
            }

            var isAppointmentExists = await _appointmentRepository.CheckAppointmentExistsAsync(
                request.PatientId,
                doctorHospital.Id,
                request.AppointmentDate,
                cancellationToken);

            if (isAppointmentExists)
            {
                return Conflict(new { message = $"Appointment already exists for Patient ID {request.PatientId}, Doctor ID {request.DoctorId}, Hospital ID {request.HospitalId} on {request.AppointmentDate}." });
            }

            var appointmentId = await _appointmentRepository.CreateAppointmentAsync(
                doctorHospital.Id,
                request.PatientId,
                request.AppointmentDate,
                request.Notes,
                cancellationToken);

            return CreatedAtAction(nameof(GetAppointment), new { id = appointmentId }, new { });
        }
        catch (DailyLimitReachedException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (DoctorHospitalNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get appointment details by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetAppointment(int id, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetAppointmentByIdAsync(id, cancellationToken);

        if (appointment == null)
        {
            return NotFound(new { message = $"Appointment with ID {id} not found." });
        }

        return Ok(appointment);
    }

    /// <summary>
    /// Get all appointments for a specific doctor at a hospital on a specific date
    /// Returns appointments ordered by serial number
    /// </summary>
    [HttpGet("doctors/{doctorId}/hospitals/{hospitalId}/date/{date}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetAppointmentsByDoctorAndDate(
        int doctorId,
        int hospitalId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var appointments = await _appointmentRepository.GetAppointmentsByDoctorAndDateAsync(
            doctorId,
            hospitalId,
            date,
            cancellationToken);

        return Ok(appointments);
    }

    /// <summary>
    /// Get appointments for a specific doctor at a hospital on a date (alternative route)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> GetAppointments(
        [FromQuery] int doctorId,
        [FromQuery] int hospitalId,
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
    {
        if (doctorId <= 0 || hospitalId <= 0)
        {
            return BadRequest(new { message = "Doctor ID and Hospital ID are required." });
        }

        var appointments = await _appointmentRepository.GetAppointmentsByDoctorAndDateAsync(
            doctorId,
            hospitalId,
            date,
            cancellationToken);

        return Ok(appointments);
    }
}
