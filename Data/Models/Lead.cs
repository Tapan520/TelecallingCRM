namespace TelecallingCRM.Data.Models;

public enum LeadStatus
{
    New,
    Contacted,
    Interested,
    NotInterested,
    FollowUp,
    Converted,
    Dead
}

public class Lead
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? AssignedToId { get; set; }
    public Guid? CampaignId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? AlternatePhone { get; set; }
    public string? Email { get; set; }
    public string? Company { get; set; }
    public string? Industry { get; set; }       // Real Estate, Insurance, EdTech …
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Notes { get; set; }
    public string? Tags { get; set; }           // comma-separated
    public LeadStatus Status { get; set; } = LeadStatus.New;
    public int Priority { get; set; } = 0;     // 0=normal, 1=high, 2=urgent
    public string? Source { get; set; }
    public string? CustomData { get; set; }    // JSON extra fields
    public int AiScore { get; set; } = 0;     // 0-100 AI conversion probability
    public string? AiInsight { get; set; }    // "Mentioned competitor", "Hot lead"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? NextFollowUpAt { get; set; }
    public DateTime? LastContactedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public AppUser? AssignedTo { get; set; }
    public Campaign? Campaign { get; set; }
    public ICollection<Call> Calls { get; set; } = new List<Call>();
    public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
    public ICollection<LeadDocument> Documents { get; set; } = new List<LeadDocument>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<WhatsAppMessage> WhatsAppMessages { get; set; } = new List<WhatsAppMessage>();
    public ICollection<SmsMessage> SmsMessages { get; set; } = new List<SmsMessage>();
    public ICollection<EmailMessage> EmailMessages { get; set; } = new List<EmailMessage>();
}

