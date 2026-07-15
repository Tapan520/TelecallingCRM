namespace TelecallingCRM.Data.Models;

public enum EmailStatus { Queued, Sent, Opened, Bounced, Failed }

public class EmailMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid SentById { get; set; }

    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;    // HTML
    public bool IsHtml { get; set; } = true;
    public string? TemplateId { get; set; }
    public EmailStatus Status { get; set; } = EmailStatus.Queued;
    public string? ProviderMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TrackingToken { get; set; }          // for open tracking
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? OpenedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Lead? Lead { get; set; }
    public AppUser SentBy { get; set; } = null!;
}

public class EmailTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
}
