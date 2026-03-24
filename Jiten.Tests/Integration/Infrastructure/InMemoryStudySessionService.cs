using System.Collections.Concurrent;
using Jiten.Api.Services;

namespace Jiten.Parser.Tests.Integration.Infrastructure;

public class InMemoryStudySessionService : IStudySessionService
{
    private readonly ConcurrentDictionary<string, string> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _reviews = new();

    public Task<string> CreateSessionAsync(string userId)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        _sessions[sessionId] = userId;
        return Task.FromResult(sessionId);
    }

    public Task<bool> ValidateSessionAsync(string sessionId, string userId)
    {
        if (!_sessions.TryGetValue(sessionId, out var owner)) return Task.FromResult(true);
        return Task.FromResult(owner == userId);
    }

    public Task<string?> GetCachedReviewResultAsync(string sessionId, string clientRequestId)
    {
        _reviews.TryGetValue($"{sessionId}:{clientRequestId}", out var result);
        return Task.FromResult(result);
    }

    public Task StoreCachedReviewResultAsync(string sessionId, string clientRequestId, string resultJson)
    {
        _reviews[$"{sessionId}:{clientRequestId}"] = resultJson;
        return Task.CompletedTask;
    }

    public Task RefreshSessionAsync(string sessionId) => Task.CompletedTask;
}
