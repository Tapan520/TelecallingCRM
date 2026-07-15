namespace TelecallingCRM.Data.Models;

public enum ActivityType
{
    LeadCreated,
    LeadUpdated,
    CallMade,
    FollowUpScheduled,
    FollowUpCompleted,
    TaskCreated,
    TaskCompleted,
    NoteAdded,
    StatusChanged,
    DocumentUploaded,
    EmailSent,
    SmsSent,
    WhatsAppSent,
    LeadAssigned,
    LeadConverted
}

public class ActivityLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid UserId { get; set; }

    public ActivityType Type { get; set; }
    public string Summary { get; set; } = string.Empty;   // e.g. "Called – Interested"
    public string? Detail { get; set; }                    // JSON or free text
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public Lead Lead { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}
