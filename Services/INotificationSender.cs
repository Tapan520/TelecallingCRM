using Microsoft.AspNetCore.SignalR;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Hubs;

namespace TelecallingCRM.Services;

/// <summary>
/// Persists a Notification to the database AND immediately pushes it to the
/// target user's SignalR connection so agents get real-time toasts without
/// waiting for the 60-second poll cycle.
/// </summary>
public interface INotificationSender
{
    Task SendAsync(Notification notification);
    Task SendManyAsync(IEnumerable<Notification> notifications);
}

public class NotificationSender : INotificationSender
{
    private readonly AppDbContext _db;
    private readonly IHubContext<CrmHub> _hub;
    private readonly ILogger<NotificationSender> _logger;

    public NotificationSender(AppDbContext db, IHubContext<CrmHub> hub, ILogger<NotificationSender> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    public async Task SendAsync(Notification notification)
    {
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();
        await PushAsync(notification);
    }

    public async Task SendManyAsync(IEnumerable<Notification> notifications)
    {
        var list = notifications.ToList();
        _db.Notifications.AddRange(list);
        await _db.SaveChangesAsync();

        foreach (var n in list)
            await PushAsync(n);
    }

    private async Task PushAsync(Notification n)
    {
        try
        {
            // Push to the specific user's SignalR connection group
            await _hub.Clients.Group($"tenant-{n.TenantId}")
                .SendAsync("NewNotification", new
                {
                    n.Id,
                    n.Type,
                    n.Title,
                    n.Body,
                    n.Link,
                    n.CreatedAt
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push SignalR notification {Id}", n.Id);
        }
    }
}
