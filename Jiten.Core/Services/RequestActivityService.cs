using Jiten.Core.Data;

namespace Jiten.Core.Services;

public class RequestActivityService(JitenDbContext context)
{
    public async Task Log(int? mediaRequestId, string userId, RequestAction action,
                          string? detail = null, string? targetUserId = null, string? ipAddress = null)
    {
        context.RequestActivityLogs.Add(new RequestActivityLog
        {
            MediaRequestId = mediaRequestId,
            UserId = userId,
            Action = action,
            Detail = detail,
            TargetUserId = targetUserId,
            IpAddress = ipAddress
        });
        await context.SaveChangesAsync();
    }

    public void LogWithoutSave(int? mediaRequestId, string userId, RequestAction action,
                               string? detail = null, string? targetUserId = null, string? ipAddress = null)
    {
        context.RequestActivityLogs.Add(new RequestActivityLog
        {
            MediaRequestId = mediaRequestId,
            UserId = userId,
            Action = action,
            Detail = detail,
            TargetUserId = targetUserId,
            IpAddress = ipAddress
        });
    }
}
