namespace DoctorAppointmentSystem.Core.Entities;

public class Doctor
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    
    // Navigation property
    public ICollection<DoctorHospital> DoctorHospitals { get; set; } = [];
}
