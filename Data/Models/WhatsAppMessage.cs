namespace TelecallingCRM.Data.Models;

public enum WhatsAppMessageDirection { Outbound, Inbound }
public enum WhatsAppMessageStatus { Queued, Sent, Delivered, Read, Failed }

public class WhatsAppMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid SentById { get; set; }

    public string ToPhone { get; set; } = string.Empty;
    public string? TemplateId { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public WhatsAppMessageDirection Direction { get; set; } = WhatsAppMessageDirection.Outbound;
    public WhatsAppMessageStatus Status { get; set; } = WhatsAppMessageStatus.Queued;
    public string? ProviderMessageId { get; set; }  // Twilio / WABA message SID
    public string? ErrorMessage { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Lead Lead { get; set; } = null!;
    public AppUser SentBy { get; set; } = null!;
}
