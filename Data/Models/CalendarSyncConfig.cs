namespace TelecallingCRM.Data.Models;

public enum CalendarProvider { Google, Outlook, iCal }
public enum CalendarSyncStatus { Connected, Disconnected, Error }

public class CalendarSyncConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    public CalendarProvider Provider { get; set; } = CalendarProvider.Google;
    public CalendarSyncStatus Status { get; set; } = CalendarSyncStatus.Disconnected;
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
    public string? CalendarId { get; set; }
    public bool SyncFollowUps { get; set; } = true;
    public bool SyncMeetings { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public AppUser User { get; set; } = null!;
}
