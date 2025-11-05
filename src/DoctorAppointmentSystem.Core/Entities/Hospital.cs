namespace DoctorAppointmentSystem.Core.Entities;

public class Hospital
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Navigation property
    public ICollection<DoctorHospital> DoctorHospitals { get; set; } = [];
}
