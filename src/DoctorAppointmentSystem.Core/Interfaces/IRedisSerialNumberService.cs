namespace DoctorAppointmentSystem.Core.Interfaces;

/// <summary>
/// Service for managing appointment serial numbers using Redis distributed locking
/// Provides atomic serial number assignment across multiple API instances with queuing behavior
/// </summary>
public interface IRedisSerialNumberService
{
    /// <summary>
    /// Gets the next serial number for a doctor-hospital-date combination
    /// Uses distributed locking to ensure atomicity across multiple API instances
    /// BLOCKS/QUEUES if another request is processing the same doctor-hospital-date (like PostgreSQL row-level lock)
    /// </summary>
    /// <param name="doctorHospitalId">The doctor-hospital association ID</param>
    /// <param name="appointmentDate">The appointment date</param>
    /// <param name="dailyLimit">The maximum daily patient limit</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The next serial number, or null if daily limit reached</returns>
    Task<int?> GetNextSerialNumberAsync(
        int doctorHospitalId, 
        DateOnly appointmentDate, 
        int dailyLimit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrements the serial number counter (used when appointment creation fails after getting serial)
    /// </summary>
    Task DecrementSerialNumberAsync(
        int doctorHospitalId, 
        DateOnly appointmentDate,
        CancellationToken cancellationToken = default);
}
