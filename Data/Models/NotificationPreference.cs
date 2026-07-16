namespace TelecallingCRM.Data.Models;

public class NotificationPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string NotificationType { get; set; } = string.Empty; // matches NotificationType enum name
    public bool InApp { get; set; } = true;
    public bool Email { get; set; } = false;
    public bool QuietHoursEnabled { get; set; } = false;
    public int QuietHoursStart { get; set; } = 22; // 24h
    public int QuietHoursEnd { get; set; } = 8;

    public AppUser User { get; set; } = null!;
}
