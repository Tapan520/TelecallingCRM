namespace TelecallingCRM.Data.Models;

public enum InvoiceStatus { Draft, Sent, Paid, Void, Overdue }

/// <summary>Invoice linked to a lead / payment (PDF-ready).</summary>
public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid CreatedById { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;   // e.g. INV-2026-0001
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public decimal SubTotal { get; set; }
    public decimal TaxPercent { get; set; } = 18m;             // GST %
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "INR";

    public string? Description { get; set; }
    public string? Notes { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueAt { get; set; }
    public DateTime? PaidAt { get; set; }

    /// <summary>Line-items serialised as JSON array of {Description, Qty, UnitPrice, Amount}.</summary>
    public string LineItemsJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public Lead Lead { get; set; } = null!;
    public Payment? Payment { get; set; }
    public AppUser CreatedBy { get; set; } = null!;
}
