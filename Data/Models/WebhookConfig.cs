namespace TelecallingCRM.Data.Models;

public enum WebhookEvent
{
    LeadCreated,
    LeadUpdated,
    LeadConverted,
    CallCompleted,
    FollowUpDue,
    TaskCompleted,
    SmsSent,
    WhatsAppSent,
    EmailSent
}

public class WebhookConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Secret { get; set; }             // HMAC signing secret
    public string Events { get; set; } = string.Empty; // JSON array of WebhookEvent names
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastTriggeredAt { get; set; }
    public int FailureCount { get; set; } = 0;

    public Tenant Tenant { get; set; } = null!;
    public ICollection<WebhookDeliveryLog> DeliveryLogs { get; set; } = new List<WebhookDeliveryLog>();
}

public class WebhookDeliveryLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WebhookId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public int HttpStatus { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;

    public WebhookConfig Webhook { get; set; } = null!;
}

public class IntegrationConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Provider { get; set; } = string.Empty; // twilio, exotel, razorpay, etc.
    public string ConfigJson { get; set; } = "{}";      // provider-specific key/value JSON
    public bool IsEnabled { get; set; } = false;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
}
