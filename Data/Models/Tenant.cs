namespace TelecallingCRM.Data.Models;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty; // subdomain identifier
    public string? LogoUrl { get; set; }
    public string Plan { get; set; } = "free"; // free, starter, pro, enterprise
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? OpenRouterApiKey { get; set; }
    public string? PreferredModel { get; set; } = "openai/gpt-4o-mini";
    public int MaxUsers { get; set; } = 5;
    public int MaxLeads { get; set; } = 500;

    public string? Industry { get; set; } // Political, Hotel, Restaurant, RealEstate, Insurance, EdTech, Travel, Hospital, BPO, NGO, Other

    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
    public ICollection<Lead> Leads { get; set; } = new List<Lead>();
    public ICollection<Campaign> Campaigns { get; set; } = new List<Campaign>();
    public ICollection<KnowledgeChunk> KnowledgeChunks { get; set; } = new List<KnowledgeChunk>();
    public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<LeadDocument> Documents { get; set; } = new List<LeadDocument>();
    public ICollection<WhatsAppMessage> WhatsAppMessages { get; set; } = new List<WhatsAppMessage>();
    public ICollection<SmsMessage> SmsMessages { get; set; } = new List<SmsMessage>();
    public ICollection<EmailMessage> EmailMessages { get; set; } = new List<EmailMessage>();
    public ICollection<EmailTemplate> EmailTemplates { get; set; } = new List<EmailTemplate>();
    public ICollection<WebhookConfig> Webhooks { get; set; } = new List<WebhookConfig>();
    public ICollection<IntegrationConfig> Integrations { get; set; } = new List<IntegrationConfig>();
    public ICollection<Meeting> Meetings { get; set; } = new List<Meeting>();
    public ICollection<Escalation> Escalations { get; set; } = new List<Escalation>();
    public ICollection<EscalationRule> EscalationRules { get; set; } = new List<EscalationRule>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<DncEntry> DncEntries { get; set; } = new List<DncEntry>();
    public ICollection<SmsTemplate> SmsTemplates { get; set; } = new List<SmsTemplate>();
    public ICollection<WhatsAppTemplate> WhatsAppTemplates { get; set; } = new List<WhatsAppTemplate>();
    public ICollection<AgentGoal> AgentGoals { get; set; } = new List<AgentGoal>();
}
