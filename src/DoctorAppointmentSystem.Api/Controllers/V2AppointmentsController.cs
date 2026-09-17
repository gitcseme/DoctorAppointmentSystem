using Microsoft.AspNetCore.Mvc;
using DoctorAppointmentSystem.Application.Services;
using DoctorAppointmentSystem.Core.DTOs;
using DoctorAppointmentSystem.Core.Exceptions;

namespace DoctorAppointmentSystem.Api.Controllers;

[ApiController]
[Route("api/v2/[controller]")]
public class V2AppointmentsController : ControllerBase
{
    private readonly IAsyncAppointmentService _service;

    public V2AppointmentsController(IAsyncAppointmentService service)
    {
        _service = service;
    }

    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Create(
        [FromBody] CreateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.CreateAsync(
                request.DoctorId,
                request.HospitalId,
                request.PatientId,
                request.AppointmentDate,
                request.Notes,
                cancellationToken);

            return Accepted(new
            {
                appointmentReference = result.AppointmentReference,
                message = result.Message,
                statusUrl = result.StatusUrl
            });
        }
        catch (EntityNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (DuplicateAppointmentException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (DailyLimitReachedException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("status/{appointmentReference}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetStatus(
        string appointmentReference,
        CancellationToken cancellationToken)
    {
        var status = await _service.GetStatusAsync(appointmentReference, cancellationToken);

        if (status == null)
        {
            return NotFound(new { message = "Appointment reference not found." });
        }

        if (status.Success)
        {
            return Ok(new
            {
                status = "Completed",
                appointmentId = status.AppointmentId,
                processedAt = status.ProcessedAt,
                appointmentUrl = $"/api/v2/appointments/{status.AppointmentId}"
            });
        }

        return Ok(new
        {
            status = "Failed",
            errorMessage = status.ErrorMessage,
            processedAt = status.ProcessedAt
        });
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var appointment = await _service.GetByIdAsync(id, cancellationToken);

        if (appointment == null)
        {
            return NotFound(new { message = $"Appointment with ID {id} not found." });
        }

        return Ok(appointment);
    }

    [HttpGet("doctors/{doctorId}/hospitals/{hospitalId}/date/{date}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetByDoctorAndDate(
        int doctorId,
        int hospitalId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var appointments = await _service.GetByDoctorAndDateAsync(
            doctorId, hospitalId, date, cancellationToken);

        return Ok(appointments);
    }

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

        var appointments = await _service.GetByDoctorAndDateAsync(
            doctorId, hospitalId, date, cancellationToken);

        return Ok(appointments);
    }
}