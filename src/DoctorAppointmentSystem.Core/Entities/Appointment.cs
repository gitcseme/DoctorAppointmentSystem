namespace DoctorAppointmentSystem.Core.Entities;

public enum AppointmentStatus
{
    Scheduled = 1,
    Completed = 2,
    Cancelled = 3,
    NoShow = 4
}

/// <summary>
/// Represents an appointment with serial-based booking
/// Serial numbers are assigned atomically per doctor per hospital per date
/// </summary>
public class Appointment
{
    public int Id { get; set; }
    public int DoctorHospitalId { get; set; }
    public int PatientId { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public int SerialNumber { get; set; }
    public AppointmentStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public DoctorHospital DoctorHospital { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
}
