using Microsoft.AspNetCore.Mvc;
using DoctorAppointmentSystem.Core.DTOs;
using DoctorAppointmentSystem.Core.Entities;
using DoctorAppointmentSystem.Core.Interfaces;
using DoctorAppointmentSystem.Core.Exceptions;

namespace DoctorAppointmentSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DoctorsController : ControllerBase
{
    private readonly IDoctorRepository _doctorRepository;
    private readonly IHospitalRepository _hospitalRepository;

    public DoctorsController(IDoctorRepository doctorRepository, IHospitalRepository hospitalRepository)
    {
        _doctorRepository = doctorRepository;
        _hospitalRepository = hospitalRepository;
    }

    /// <summary>
    /// Create a new doctor
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DoctorResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DoctorResponse>> CreateDoctor([FromBody] CreateDoctorRequest request, CancellationToken cancellationToken)
    {
        var doctor = new Doctor
        {
       Name = request.Name,
 Specialization = request.Specialization,
 Email = request.Email,
            PhoneNumber = request.PhoneNumber
        };

        var createdDoctor = await _doctorRepository.CreateAsync(doctor, cancellationToken);

        var response = new DoctorResponse(
createdDoctor.Id,
  createdDoctor.Name,
          createdDoctor.Specialization,
    createdDoctor.Email,
 createdDoctor.PhoneNumber
        );

        return CreatedAtAction(nameof(GetDoctor), new { id = createdDoctor.Id }, response);
 }

    /// <summary>
    /// Get a doctor by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(DoctorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DoctorResponse>> GetDoctor(int id, CancellationToken cancellationToken)
    {
        var doctor = await _doctorRepository.GetByIdAsync(id, cancellationToken);

        if (doctor == null)
        {
     return NotFound(new { message = $"Doctor with ID {id} not found." });
        }

        var response = new DoctorResponse(
            doctor.Id,
            doctor.Name,
            doctor.Specialization,
            doctor.Email,
            doctor.PhoneNumber
   );

     return Ok(response);
    }

    /// <summary>
    /// Get all doctors
    /// </summary>
  [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DoctorResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DoctorResponse>>> GetAllDoctors(CancellationToken cancellationToken)
    {
      var doctors = await _doctorRepository.GetAllAsync(cancellationToken);

    var response = doctors.Select(d => new DoctorResponse(
       d.Id,
            d.Name,
         d.Specialization,
 d.Email,
            d.PhoneNumber
 ));

        return Ok(response);
    }

    /// <summary>
    /// Assign a doctor to a hospital with a daily patient limit
    /// </summary>
    [HttpPost("assign-to-hospital")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> AssignDoctorToHospital([FromBody] AssignDoctorToHospitalRequest request, CancellationToken cancellationToken)
    {
        // Validate doctor exists
        var doctor = await _doctorRepository.GetByIdAsync(request.DoctorId, cancellationToken);
        if (doctor == null)
        {
       return NotFound(new { message = $"Doctor with ID {request.DoctorId} not found." });
        }

        // Validate hospital exists
        var hospital = await _hospitalRepository.GetByIdAsync(request.HospitalId, cancellationToken);
      if (hospital == null)
        {
  return NotFound(new { message = $"Hospital with ID {request.HospitalId} not found." });
        }

        // Check if already assigned
        var existing = await _doctorRepository.GetDoctorHospitalAsync(request.DoctorId, request.HospitalId, cancellationToken);
     if (existing != null)
     {
   return BadRequest(new { message = "Doctor is already assigned to this hospital." });
   }

        await _doctorRepository.AssignToHospitalAsync(request.DoctorId, request.HospitalId, request.DailyPatientLimit, cancellationToken);

  return Ok(new { message = "Doctor successfully assigned to hospital." });
    }

    /// <summary>
    /// Get doctor-hospital association details
    /// </summary>
    [HttpGet("{doctorId}/hospitals/{hospitalId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetDoctorHospital(int doctorId, int hospitalId, CancellationToken cancellationToken)
    {
        var doctorHospital = await _doctorRepository.GetDoctorHospitalAsync(doctorId, hospitalId, cancellationToken);

        if (doctorHospital == null)
        {
         return NotFound(new { message = "Doctor-Hospital association not found." });
        }

  return Ok(new
    {
            doctorHospital.Id,
    Doctor = new
      {
           doctorHospital.Doctor.Id,
    doctorHospital.Doctor.Name,
     doctorHospital.Doctor.Specialization
            },
            Hospital = new
       {
           doctorHospital.Hospital.Id,
          doctorHospital.Hospital.Name,
             doctorHospital.Hospital.City
        },
            doctorHospital.DailyPatientLimit,
    doctorHospital.CreatedAt
      });
  }
}
