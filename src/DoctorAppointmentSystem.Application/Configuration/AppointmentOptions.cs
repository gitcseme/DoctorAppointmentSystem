namespace DoctorAppointmentSystem.Application.Configuration;

public class AppointmentOptions
{
    public const string SectionName = "Appointment";
    
    public string Mode { get; set; } = "Sync";     // Sync | Async
    public string Provider { get; set; } = "Postgres";  // Postgres | Redis (Sync), RabbitMq (Async)
}

public enum AppointmentMode
{
    Sync,
    Async
}

public enum AppointmentProvider
{
    Postgres,
    Redis,
    RabbitMq
}