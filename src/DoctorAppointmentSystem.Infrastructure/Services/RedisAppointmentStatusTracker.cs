using System.Text.Json;
using StackExchange.Redis;
using DoctorAppointmentSystem.Core.DTOs;
using DoctorAppointmentSystem.Core.Interfaces;

namespace DoctorAppointmentSystem.Infrastructure.Services;

/// <summary>
/// Tracks appointment processing status in Redis with TTL
/// </summary>
public class RedisAppointmentStatusTracker : IAppointmentStatusTracker
{
    private readonly IConnectionMultiplexer _redis;
    private readonly TimeSpan _statusTtl = TimeSpan.FromHours(24); // Keep status for 24 hours

    public RedisAppointmentStatusTracker(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    private string GetKey(string appointmentReference) => $"appointment:status:{appointmentReference}";

    public async Task SetProcessingAsync(string appointmentReference, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var result = new AppointmentProcessingResult
        {
            AppointmentReference = appointmentReference,
            Success = false,
            ProcessedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(result);
        await db.StringSetAsync(GetKey(appointmentReference), json, _statusTtl);
    }

    public async Task SetCompletedAsync(
        string appointmentReference, 
        int appointmentId, 
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var result = new AppointmentProcessingResult
        {
            AppointmentReference = appointmentReference,
            Success = true,
            AppointmentId = appointmentId,
            ProcessedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(result);
        await db.StringSetAsync(GetKey(appointmentReference), json, _statusTtl);
    }

    public async Task SetFailedAsync(
        string appointmentReference, 
        string errorMessage, 
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var result = new AppointmentProcessingResult
        {
            AppointmentReference = appointmentReference,
            Success = false,
            ErrorMessage = errorMessage,
            ProcessedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(result);
        await db.StringSetAsync(GetKey(appointmentReference), json, _statusTtl);
    }

    public async Task<AppointmentProcessingResult?> GetStatusAsync(
        string appointmentReference, 
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var json = await db.StringGetAsync(GetKey(appointmentReference));

        if (json.IsNullOrEmpty)
            return null;

        return JsonSerializer.Deserialize<AppointmentProcessingResult>(json!);
    }
}
