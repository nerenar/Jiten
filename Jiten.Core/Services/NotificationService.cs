using Jiten.Core.Data;

namespace Jiten.Core.Services;

public class NotificationService(JitenDbContext context)
{
    public async Task Notify(string userId, NotificationType type, string title, string message, string? linkUrl = null)
    {
        context.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            LinkUrl = linkUrl
        });
        await context.SaveChangesAsync();
    }

    public async Task NotifyMany(IEnumerable<string> userIds, NotificationType type, string title, string message, string? linkUrl = null)
    {
        var distinct = userIds.Distinct().ToList();
        if (distinct.Count == 0) return;

        foreach (var userId in distinct)
        {
            context.Notifications.Add(new Notification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                LinkUrl = linkUrl
            });
        }

        await context.SaveChangesAsync();
    }
}
