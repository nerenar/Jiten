namespace Jiten.Api.Integrations;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides a shared rate limiter for JPDB API requests across multiple client instances
/// </summary>
public static class JpdbRateLimiter
{
    private static readonly SemaphoreSlim RateLimitSemaphore = new SemaphoreSlim(1, 1);
    private static DateTime _lastRequestTime = DateTime.MinValue;
    private static readonly TimeSpan RateLimitDelay = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// Executes an API request with rate limiting applied
    /// </summary>
    /// <param name="apiAction">The API request action to execute</param>
    /// <returns>The result of the API request</returns>
    public static async Task<T> ExecuteWithRateLimitAsync<T>(Func<Task<T>> apiAction)
    {
        await RateLimitSemaphore.WaitAsync();

        try
        {
            // Enforce rate limit
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLastRequest < RateLimitDelay)
            {
                var delayTime = RateLimitDelay - timeSinceLastRequest;
                await Task.Delay(delayTime);
            }

            var result = await apiAction();
            _lastRequestTime = DateTime.UtcNow;
            return result;
        }
        finally
        {
            RateLimitSemaphore.Release();
        }
    }
}
