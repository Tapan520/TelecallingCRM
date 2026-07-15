namespace TelecallingCRM.Data.Models;

public enum FollowUpChannel { Call, WhatsApp, SMS, Email, Meeting, Other }
public enum FollowUpStatus { Pending, Done, Missed, Cancelled }

public class FollowUp
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid AssignedToId { get; set; }

    public DateTime ScheduledAt { get; set; }
    public FollowUpChannel Channel { get; set; } = FollowUpChannel.Call;
    public FollowUpStatus Status { get; set; } = FollowUpStatus.Pending;
    public string? Notes { get; set; }
    public bool IsRecurring { get; set; } = false;
    public string? RecurrenceRule { get; set; } // e.g. "DAILY", "WEEKLY"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Lead Lead { get; set; } = null!;
    public AppUser AssignedTo { get; set; } = null!;
}
