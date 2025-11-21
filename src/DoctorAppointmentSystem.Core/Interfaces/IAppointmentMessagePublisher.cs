using DoctorAppointmentSystem.Core.DTOs;

namespace DoctorAppointmentSystem.Core.Interfaces;

/// <summary>
/// Service for publishing appointment creation messages to RabbitMQ
/// </summary>
public interface IAppointmentMessagePublisher
{
    /// <summary>
    /// Publishes an appointment creation message to the queue
    /// </summary>
    Task PublishAppointmentCreationAsync(
        AppointmentCreationMessage message, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for tracking appointment processing status in Redis
/// </summary>
public interface IAppointmentStatusTracker
{
    /// <summary>
    /// Stores the initial status as "Processing"
    /// </summary>
    Task SetProcessingAsync(string appointmentReference, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates status when appointment is successfully created
    /// </summary>
    Task SetCompletedAsync(string appointmentReference, int appointmentId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates status when appointment creation fails
    /// </summary>
    Task SetFailedAsync(string appointmentReference, string errorMessage, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the current status and result
    /// </summary>
    Task<AppointmentProcessingResult?> GetStatusAsync(string appointmentReference, CancellationToken cancellationToken = default);
}
