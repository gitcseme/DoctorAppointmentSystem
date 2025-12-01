using Microsoft.AspNetCore.Mvc;
using DoctorAppointmentSystem.Core.DTOs;
using DoctorAppointmentSystem.Core.Interfaces;
using DoctorAppointmentSystem.Core.Exceptions;

namespace DoctorAppointmentSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentWriteRepository _writeRepository;
    private readonly IAppointmentReadRepository _readRepository;
    private readonly IDoctorRepository _doctorRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly IAppointmentStatusTracker _statusTracker;

    public AppointmentsController(
        IAppointmentWriteRepository writeRepository,
        IAppointmentReadRepository readRepository,
        IDoctorRepository doctorRepository,
        IPatientRepository patientRepository,
        IAppointmentStatusTracker statusTracker)
    {
        _writeRepository = writeRepository;
        _readRepository = readRepository;
        _doctorRepository = doctorRepository;
        _patientRepository = patientRepository;
        _statusTracker = statusTracker;
    }

    /// <summary>
    /// Create a new appointment (queued for async processing via RabbitMQ)
    /// Returns appointment reference for status tracking
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> CreateAppointment(
        [FromBody] CreateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var patient = await _patientRepository.GetByIdAsync(request.PatientId, cancellationToken);
        if (patient == null)
        {
            return NotFound(new { message = $"Patient with ID {request.PatientId} not found." });
        }

        var doctorHospital = await _doctorRepository.GetDoctorHospitalAsync(
            request.DoctorId,
            request.HospitalId,
            cancellationToken);

        if (doctorHospital == null)
        {
            return NotFound(new
            {
                message = $"Doctor with ID {request.DoctorId} is not associated with Hospital ID {request.HospitalId}."
            });
        }

        // Check for existing appointment in PostgreSQL
        var appointExists = await _readRepository.CheckAppointmentExistsAsync(
            request.PatientId,
            doctorHospital.Id,
            request.AppointmentDate,
            cancellationToken);

        if (appointExists)
        {
            return Conflict(new
            {
                message = $"Appointment already exists for this patient with this doctor on {request.AppointmentDate}."
            });
        }

        var appointmentInFlightRef = Guid.NewGuid().ToString("N");

        // Mark as in-flight to prevent concurrent duplicate requests
        var markedAsInFlight = await _statusTracker.MarkAsInFlightAsync(
            request.PatientId,
            doctorHospital.Id,
            request.AppointmentDate,
            appointmentInFlightRef,
            cancellationToken);

        if (!markedAsInFlight)
        {
            return Conflict(new
            {
                message = $"An appointment request for this patient with this doctor on {request.AppointmentDate} is already being processed."
            });
        }

        // Create appointment (queued to RabbitMQ)
        var appointmentReference = await _writeRepository.CreateAppointmentAsync(
            doctorHospital,
            request.PatientId,
            request.AppointmentDate,
            request.Notes,
            cancellationToken);

        return Accepted(new
        {
            appointmentReference,
            status = "Processing",
            message = "Appointment is being created. Use the reference to check status.",
            statusUrl = $"/api/appointments/status/{appointmentReference}"
        });
    }

    /// <summary>
    /// Get appointment processing status by reference
    /// </summary>
    [HttpGet("status/{appointmentReference}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetAppointmentStatus(
        string appointmentReference,
        CancellationToken cancellationToken)
    {
        var status = await _statusTracker.GetStatusAsync(appointmentReference, cancellationToken);

        if (status == null)
        {
            return NotFound(new { message = "Appointment reference not found." });
        }

        if (status.Success && status.AppointmentId.HasValue)
        {
            return Ok(new
            {
                status = "Completed",
                appointmentId = status.AppointmentId.Value,
                processedAt = status.ProcessedAt,
                appointmentUrl = $"/api/appointments/{status.AppointmentId.Value}"
            });
        }
        else if (!status.Success && !string.IsNullOrEmpty(status.ErrorMessage))
        {
            return Ok(new
            {
                status = "Failed",
                errorMessage = status.ErrorMessage,
                processedAt = status.ProcessedAt
            });
        }

        return Ok(new
        {
            status = "Processing",
            message = "Appointment is being created..."
        });
    }

    /// <summary>
    /// Get appointment details by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetAppointment(int id, CancellationToken cancellationToken)
    {
        var appointment = await _readRepository.GetAppointmentByIdAsync(id, cancellationToken);

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
        var appointments = await _readRepository.GetAppointmentsByDoctorAndDateAsync(
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

        var appointments = await _readRepository.GetAppointmentsByDoctorAndDateAsync(
            doctorId,
            hospitalId,
            date,
            cancellationToken);

        return Ok(appointments);
    }
}
