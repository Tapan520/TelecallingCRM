using Microsoft.AspNetCore.Identity;

namespace TelecallingCRM.Data.Models;

public class AppUser : IdentityUser<Guid>
{
    public Guid? TenantId { get; set; }          // null for superadmin
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "agent"; // superadmin, admin, manager, agent
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }

    public Tenant? Tenant { get; set; }
    public ICollection<Call> Calls { get; set; } = new List<Call>();
    public ICollection<Lead> AssignedLeads { get; set; } = new List<Lead>();
    public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();
    public ICollection<TaskItem> CreatedTasks { get; set; } = new List<TaskItem>();
    public ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<LeadDocument> UploadedDocuments { get; set; } = new List<LeadDocument>();
    public ICollection<WhatsAppMessage> WhatsAppMessages { get; set; } = new List<WhatsAppMessage>();
    public ICollection<SmsMessage> SmsMessages { get; set; } = new List<SmsMessage>();
    public ICollection<EmailMessage> EmailMessages { get; set; } = new List<EmailMessage>();
    public ICollection<Meeting> OrganisedMeetings { get; set; } = new List<Meeting>();
    public ICollection<MeetingAttendee> MeetingAttendances { get; set; } = new List<MeetingAttendee>();
    public ICollection<Escalation> EscalationsReceived { get; set; } = new List<Escalation>();
    public ICollection<Escalation> EscalationsAssigned { get; set; } = new List<Escalation>();
    public ICollection<EscalationRule> EscalationRules { get; set; } = new List<EscalationRule>();
    public ICollection<Payment> RecordedPayments { get; set; } = new List<Payment>();
    public ICollection<CallControlEvent> CallControlEvents { get; set; } = new List<CallControlEvent>();
    public ICollection<DncEntry> DncEntries { get; set; } = new List<DncEntry>();
    public ICollection<AgentGoal> AgentGoals { get; set; } = new List<AgentGoal>();
    public ICollection<AgentGoal> CreatedGoals { get; set; } = new List<AgentGoal>();
}
