namespace TelecallingCRM.Data.Models;

public enum SmsMessageStatus { Queued, Sent, Delivered, Failed }
public enum SmsMessageType { Transactional, Promotional, OTP, Bulk }

public class SmsMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid SentById { get; set; }

    public string ToPhone { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public SmsMessageType Type { get; set; } = SmsMessageType.Transactional;
    public SmsMessageStatus Status { get; set; } = SmsMessageStatus.Queued;
    public string? ProviderMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Lead? Lead { get; set; }
    public AppUser SentBy { get; set; } = null!;
}
