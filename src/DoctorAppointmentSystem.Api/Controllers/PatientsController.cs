using Microsoft.AspNetCore.Mvc;
using DoctorAppointmentSystem.Core.DTOs;
using DoctorAppointmentSystem.Core.Entities;
using DoctorAppointmentSystem.Core.Interfaces;

namespace DoctorAppointmentSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly IPatientRepository _patientRepository;

    public PatientsController(IPatientRepository patientRepository)
    {
        _patientRepository = patientRepository;
    }

    /// <summary>
    /// Create a new patient
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PatientResponse>> CreatePatient([FromBody] CreatePatientRequest request, CancellationToken cancellationToken)
    {
        var patient = new Patient
     {
       Name = request.Name,
        Email = request.Email,
  PhoneNumber = request.PhoneNumber,
            DateOfBirth = request.DateOfBirth,
    Address = request.Address
  };

 var createdPatient = await _patientRepository.CreateAsync(patient, cancellationToken);

     var response = new PatientResponse(
          createdPatient.Id,
            createdPatient.Name,
  createdPatient.Email,
   createdPatient.PhoneNumber,
   createdPatient.DateOfBirth,
        createdPatient.Address
     );

   return CreatedAtAction(nameof(GetPatient), new { id = createdPatient.Id }, response);
    }

    /// <summary>
    /// Get a patient by ID
 /// </summary>
    [HttpGet("{id}")]
  [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatientResponse>> GetPatient(int id, CancellationToken cancellationToken)
    {
  var patient = await _patientRepository.GetByIdAsync(id, cancellationToken);

        if (patient == null)
        {
          return NotFound(new { message = $"Patient with ID {id} not found." });
        }

  var response = new PatientResponse(
       patient.Id,
  patient.Name,
     patient.Email,
    patient.PhoneNumber,
   patient.DateOfBirth,
        patient.Address
   );

        return Ok(response);
    }

    /// <summary>
  /// Get all patients
 /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PatientResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PatientResponse>>> GetAllPatients(CancellationToken cancellationToken)
    {
   var patients = await _patientRepository.GetAllAsync(cancellationToken);

        var response = patients.Select(p => new PatientResponse(
            p.Id,
 p.Name,
            p.Email,
p.PhoneNumber,
p.DateOfBirth,
 p.Address
        ));

        return Ok(response);
    }
}
