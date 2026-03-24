using StackExchange.Redis;

namespace Jiten.Api.Services;

public class StudySessionService(IConnectionMultiplexer redis, ILogger<StudySessionService> logger) : IStudySessionService
{
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(2);
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromMinutes(10);

    public async Task<string> CreateSessionAsync(string userId)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        try
        {
            var db = redis.GetDatabase();
            await db.StringSetAsync($"srs:session:{sessionId}", userId, SessionTtl);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to store study session in Redis");
        }
        return sessionId;
    }

    public async Task<bool> ValidateSessionAsync(string sessionId, string userId)
    {
        try
        {
            var db = redis.GetDatabase();
            var stored = await db.StringGetAsync($"srs:session:{sessionId}");
            if (stored.IsNullOrEmpty) return true;
            return stored == userId;
        }
        catch
        {
            return true;
        }
    }

    public async Task<string?> GetCachedReviewResultAsync(string sessionId, string clientRequestId)
    {
        try
        {
            var db = redis.GetDatabase();
            var cached = await db.StringGetAsync($"srs:review:{sessionId}:{clientRequestId}");
            return cached.IsNullOrEmpty ? null : (string?)cached;
        }
        catch
        {
            return null;
        }
    }

    public async Task StoreCachedReviewResultAsync(string sessionId, string clientRequestId, string resultJson)
    {
        try
        {
            var db = redis.GetDatabase();
            await db.StringSetAsync($"srs:review:{sessionId}:{clientRequestId}", resultJson, IdempotencyTtl);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to cache review result in Redis");
        }
    }

    public async Task RefreshSessionAsync(string sessionId)
    {
        try
        {
            var db = redis.GetDatabase();
            await db.KeyExpireAsync($"srs:session:{sessionId}", SessionTtl);
        }
        catch { /* best effort */ }
    }
}
