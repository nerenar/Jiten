using System.Collections.Concurrent;

namespace Jiten.Api.Services;

public class ParseThrottleService : IParseThrottleService
{
    private const int BudgetPerWindow = 200_000;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, UserBucket> _buckets = new();
    private DateTime _lastCleanup = DateTime.UtcNow;
    private readonly Lock _cleanupLock = new();

    public bool TryConsume(string userId, int characterCount)
    {
        var now = DateTime.UtcNow;
        CleanupIfNeeded(now);

        var bucket = _buckets.GetOrAdd(userId, _ => new UserBucket(BudgetPerWindow, now));

        lock (bucket)
        {
            if (now - bucket.WindowStart >= Window)
            {
                bucket.Remaining = BudgetPerWindow;
                bucket.WindowStart = now;
            }

            if (bucket.Remaining < characterCount)
                return false;

            bucket.Remaining -= characterCount;
            return true;
        }
    }

    private void CleanupIfNeeded(DateTime now)
    {
        if (now - _lastCleanup < CleanupInterval)
            return;

        lock (_cleanupLock)
        {
            if (now - _lastCleanup < CleanupInterval)
                return;

            var cutoff = now - Window - TimeSpan.FromMinutes(1);
            var keysToRemove = _buckets
                .Where(kvp => { lock (kvp.Value) { return kvp.Value.WindowStart < cutoff; } })
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
                _buckets.TryRemove(key, out _);

            _lastCleanup = now;
        }
    }

    private class UserBucket(int remaining, DateTime windowStart)
    {
        public int Remaining = remaining;
        public DateTime WindowStart = windowStart;
    }
}
