namespace Jiten.Api.Services;

public interface IStudySessionService
{
    Task<string> CreateSessionAsync(string userId);
    Task<bool> ValidateSessionAsync(string sessionId, string userId);
    Task<string?> GetCachedReviewResultAsync(string sessionId, string clientRequestId);
    Task StoreCachedReviewResultAsync(string sessionId, string clientRequestId, string resultJson);
    Task RefreshSessionAsync(string sessionId);
}
