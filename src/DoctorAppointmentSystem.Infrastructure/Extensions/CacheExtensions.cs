using Microsoft.Extensions.Caching.Distributed;

namespace DoctorAppointmentSystem.Infrastructure.Extensions;

public static class CacheExtensions
{
    public static T? GetOrCreate<T>(this IDistributedCache cache, string cacheKey, Func<DistributedCacheEntryOptions, T?> factory)
    {
        var cachedData = cache.Get(cacheKey);
        if (cachedData != null)
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(cachedData);
        }

        var options = new DistributedCacheEntryOptions();

        T? data = factory(options);
        cache.SetString(cacheKey, System.Text.Json.JsonSerializer.Serialize(data), options);

        return data;
    }

    public static async Task<T?> GetOrCreateAsync<T>(this IDistributedCache cache, string cacheKey, 
        Func<DistributedCacheEntryOptions, Task<T?>> factory)
    {
        var cachedData = cache.Get(cacheKey);
        if (cachedData != null)
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(cachedData);
        }

        var options = new DistributedCacheEntryOptions();

        T? data = await factory(options);
        cache.SetString(cacheKey, System.Text.Json.JsonSerializer.Serialize(data), options);

        return data;
    }
}
