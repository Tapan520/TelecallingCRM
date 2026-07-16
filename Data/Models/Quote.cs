namespace TelecallingCRM.Data.Models;

public enum QuoteStatus { Draft, Sent, Accepted, Rejected, Expired }

public class Quote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid? DealId { get; set; }
    public Guid CreatedById { get; set; }

    public string QuoteNumber { get; set; } = string.Empty; // QT-2026-0001
    public QuoteStatus Status { get; set; } = QuoteStatus.Draft;
    public string? Title { get; set; }

    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; } = 0;
    public decimal TaxPercent { get; set; } = 18;
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "INR";

    public string? Notes { get; set; }
    /// <summary>JSON: [{Description, Qty, UnitPrice, Amount}]</summary>
    public string LineItemsJson { get; set; } = "[]";
    public DateTime? ExpiresAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public Lead Lead { get; set; } = null!;
    public Deal? Deal { get; set; }
    public AppUser CreatedBy { get; set; } = null!;
}
