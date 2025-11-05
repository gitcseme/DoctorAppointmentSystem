namespace DoctorAppointmentSystem.Core.Exceptions;

public class AppointmentException : Exception
{
    public AppointmentException(string message) : base(message)
    {
    }
}

public class DailyLimitReachedException : AppointmentException
{
    public DailyLimitReachedException(string message) : base(message)
    {
    }
}

public class DoctorHospitalNotFoundException : AppointmentException
{
 public DoctorHospitalNotFoundException(string message) : base(message)
    {
}
}

public class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string message) : base(message)
    {
    }
}
