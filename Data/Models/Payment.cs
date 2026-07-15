namespace TelecallingCRM.Data.Models;

public enum PaymentStatus { Pending, Captured, Refunded, Failed }

/// <summary>Represents a payment transaction linked to a lead (Razorpay or manual).</summary>
public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid RecordedById { get; set; }

    /// <summary>Razorpay order ID created via API.</summary>
    public string? RazorpayOrderId { get; set; }
    /// <summary>Razorpay payment ID returned after capture.</summary>
    public string? RazorpayPaymentId { get; set; }
    /// <summary>Razorpay signature for verification.</summary>
    public string? RazorpaySignature { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? Description { get; set; }
    public string? ReceiptNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CapturedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Lead Lead { get; set; } = null!;
    public AppUser RecordedBy { get; set; } = null!;
}
