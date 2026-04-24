namespace Jiten.Api.Services;

public interface IStudySessionService
{
    Task<string> CreateSession(string userId);
    Task<bool> ValidateSession(string sessionId, string userId);
    Task<string?> GetCachedReviewResult(string sessionId, string clientRequestId);
    Task StoreCachedReviewResult(string sessionId, string clientRequestId, string resultJson);
    Task RefreshSession(string sessionId);
    Task<long> BumpStudyOverviewVersion(string userId);
    Task<long> GetStudyOverviewVersion(string userId);
}
