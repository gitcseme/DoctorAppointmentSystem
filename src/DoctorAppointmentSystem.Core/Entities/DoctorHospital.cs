namespace DoctorAppointmentSystem.Core.Entities;

/// <summary>
/// Represents the many-to-many relationship between Doctor and Hospital
/// Each doctor can work at multiple hospitals with different daily patient limits
/// </summary>
public class DoctorHospital
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public int HospitalId { get; set; }
    public int DailyPatientLimit { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Doctor Doctor { get; set; } = null!;
    public Hospital Hospital { get; set; } = null!;
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
