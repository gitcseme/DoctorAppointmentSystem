using Microsoft.AspNetCore.Mvc;
using DoctorAppointmentSystem.Core.DTOs;
using DoctorAppointmentSystem.Core.Entities;
using DoctorAppointmentSystem.Core.Interfaces;

namespace DoctorAppointmentSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HospitalsController : ControllerBase
{
    private readonly IHospitalRepository _hospitalRepository;

    public HospitalsController(IHospitalRepository hospitalRepository)
    {
        _hospitalRepository = hospitalRepository;
    }

 /// <summary>
    /// Create a new hospital
/// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(HospitalResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HospitalResponse>> CreateHospital([FromBody] CreateHospitalRequest request, CancellationToken cancellationToken)
  {
  var hospital = new Hospital
        {
            Name = request.Name,
 Address = request.Address,
            City = request.City,
       PhoneNumber = request.PhoneNumber
    };

      var createdHospital = await _hospitalRepository.CreateAsync(hospital, cancellationToken);

        var response = new HospitalResponse(
         createdHospital.Id,
          createdHospital.Name,
            createdHospital.Address,
    createdHospital.City,
          createdHospital.PhoneNumber
        );

        return CreatedAtAction(nameof(GetHospital), new { id = createdHospital.Id }, response);
    }

    /// <summary>
    /// Get a hospital by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(HospitalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HospitalResponse>> GetHospital(int id, CancellationToken cancellationToken)
    {
        var hospital = await _hospitalRepository.GetByIdAsync(id, cancellationToken);

        if (hospital == null)
        {
            return NotFound(new { message = $"Hospital with ID {id} not found." });
        }

     var response = new HospitalResponse(
 hospital.Id,
            hospital.Name,
      hospital.Address,
            hospital.City,
            hospital.PhoneNumber
        );

        return Ok(response);
    }

    /// <summary>
    /// Get all hospitals
 /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<HospitalResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<HospitalResponse>>> GetAllHospitals(CancellationToken cancellationToken)
    {
     var hospitals = await _hospitalRepository.GetAllAsync(cancellationToken);

        var response = hospitals.Select(h => new HospitalResponse(
      h.Id,
          h.Name,
            h.Address,
    h.City,
            h.PhoneNumber
    ));

    return Ok(response);
  }

    /// <summary>
    /// Get all doctors working at a specific hospital
    /// </summary>
    [HttpGet("{id}/doctors")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetHospitalDoctors(int id, CancellationToken cancellationToken)
    {
   var hospital = await _hospitalRepository.GetByIdAsync(id, cancellationToken);

        if (hospital == null)
        {
return NotFound(new { message = $"Hospital with ID {id} not found." });
        }

        var doctors = hospital.DoctorHospitals.Select(dh => new
        {
         dh.Doctor.Id,
dh.Doctor.Name,
            dh.Doctor.Specialization,
      dh.DailyPatientLimit
    });

        return Ok(doctors);
    }
}
