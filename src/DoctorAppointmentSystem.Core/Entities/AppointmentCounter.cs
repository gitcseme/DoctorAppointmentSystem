namespace DoctorAppointmentSystem.Core.Entities;

/// <summary>
/// Tracks appointment serial numbers and counts for each doctor-hospital-date combination
/// This enables efficient atomic operations with single-row locking
/// </summary>
public class AppointmentCounter
{
    public int Id { get; set; }
    public int DoctorHospitalId { get; set; }
    public DateOnly AppointmentDate { get; set; }
  
    /// <summary>
    /// The last assigned serial number for this doctor-hospital-date
    /// Next appointment will get CurrentSerial + 1
    /// </summary>
    public int CurrentSerial { get; set; }
    
    /// <summary>
    /// Total count of non-cancelled appointments for this doctor-hospital-date
    /// Used for checking against DailyPatientLimit
    /// </summary>
    public int AppointmentCount { get; set; }
    
    public DateTime UpdatedAt { get; set; }

    // Navigation property
    public DoctorHospital DoctorHospital { get; set; } = null!;
}
