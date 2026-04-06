namespace DoctorAppointmentSystem.Core.DTOs;

/// <summary>
/// Message published to RabbitMQ when an appointment is queued for creation
/// </summary>
public class AppointmentCreationMessage
{
    /// <summary>
    /// Unique reference for tracking this appointment request
    /// </summary>
    public string AppointmentReference { get; set; } = null!;
    
    public int DoctorHospitalId { get; set; }
    public int PatientId { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public int SerialNumber { get; set; }
    public string? Notes { get; set; }
    public DateTime QueuedAt { get; set; }
}

/// <summary>
/// Result of appointment processing
/// </summary>
public class AppointmentProcessingResult
{
    public string AppointmentReference { get; set; } = null!;
    public bool Success { get; set; }
    public int? AppointmentId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ProcessedAt { get; set; }
}
