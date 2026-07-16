namespace TelecallingCRM.Data.Models;

public enum NotificationType
{
    FollowUpDue,
    TaskDue,
    TaskOverdue,
    NewLeadAssigned,
    CallMissed,
    LeadConverted,
    MeetingDue,
    SystemAlert
}

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? Link { get; set; }           // e.g. "/Leads/Index?id=..."
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}
