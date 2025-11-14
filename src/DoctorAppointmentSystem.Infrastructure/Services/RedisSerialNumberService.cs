using DoctorAppointmentSystem.Core.Interfaces;
using Medallion.Threading;
using StackExchange.Redis;

namespace DoctorAppointmentSystem.Infrastructure.Services;

public class RedisSerialNumberService : IRedisSerialNumberService
{
    private readonly IDatabase _db;
    private readonly IDistributedLockProvider _distributedLockProvider;
    
    private static readonly TimeSpan MaxWaitTime = TimeSpan.FromSeconds(30);
    
    private const string CounterKeyPrefix = "appt:serial";
    private const string LockKeyPrefix = "appt:lock";

    public RedisSerialNumberService(IConnectionMultiplexer redis, IDistributedLockProvider distributedLockProvider)
    {
        _db = redis.GetDatabase();
        _distributedLockProvider = distributedLockProvider;
    }

    public async Task<int?> GetNextSerialNumberAsync(
        int doctorHospitalId, 
        DateOnly appointmentDate, 
        int dailyLimit,
        CancellationToken cancellationToken = default)
    {
        var lockKey = GetLockKey(doctorHospitalId, appointmentDate);
        var counterKey = GetCounterKey(doctorHospitalId, appointmentDate);

        // Subsequent requests will wait in queue for the lock
        await using var acquiredLock = await _distributedLockProvider.TryAcquireLockAsync(lockKey, 
            MaxWaitTime,
            cancellationToken);

        if (acquiredLock is null)
        {
            throw new TimeoutException(
                $"Failed to acquire lock for doctor-hospital {doctorHospitalId} on {appointmentDate} within {MaxWaitTime.TotalSeconds}s. Too many concurrent requests.");
        }

        try
        {
            var newSerial = await _db.StringIncrementAsync(counterKey);

            if (newSerial > dailyLimit)
            {
                await _db.StringDecrementAsync(counterKey);
                return null;
            }

            var expiryTime = GetMidnightExpiry(appointmentDate);
            await _db.KeyExpireAsync(counterKey, expiryTime);

            return (int)newSerial;
        }
        finally
        {
            await acquiredLock.DisposeAsync();
        }
    }

    public async Task DecrementSerialNumberAsync(
        int doctorHospitalId, 
        DateOnly appointmentDate,
        CancellationToken cancellationToken = default)
    {
        var counterKey = GetCounterKey(doctorHospitalId, appointmentDate);
        await _db.StringDecrementAsync(counterKey);
    }

    private static string GetCounterKey(int doctorHospitalId, DateOnly appointmentDate)
    {
        return $"{CounterKeyPrefix}:{doctorHospitalId}:{appointmentDate:yyyy-MM-dd}";
    }

    private static string GetLockKey(int doctorHospitalId, DateOnly appointmentDate)
    {
        return $"{LockKeyPrefix}:{doctorHospitalId}:{appointmentDate:yyyy-MM-dd}";
    }


    // Set expiration to midnight of next day (auto-cleanup)
    private static TimeSpan GetMidnightExpiry(DateOnly appointmentDate)
    {
        var midnight = appointmentDate.ToDateTime(TimeOnly.MinValue).AddDays(1);
        var now = DateTime.UtcNow;
        var timeUntilMidnight = midnight - now;
        
        // Ensure at least 1 hour expiry to handle timezone issues
        return timeUntilMidnight > TimeSpan.FromHours(1) 
            ? timeUntilMidnight 
            : TimeSpan.FromHours(24);
    }
}
