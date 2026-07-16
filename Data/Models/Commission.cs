namespace TelecallingCRM.Data.Models;

public enum CommissionType { PercentOfPayment, FlatPerConversion }
public enum CommissionStatus { Pending, Approved, Paid }

public class CommissionRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;
    public CommissionType Type { get; set; } = CommissionType.PercentOfPayment;
    /// <summary>Percent (e.g. 5.0) or flat amount.</summary>
    public decimal Value { get; set; } = 0;
    public Guid? CampaignId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public Campaign? Campaign { get; set; }
    public ICollection<CommissionEntry> Entries { get; set; } = new List<CommissionEntry>();
}

/// <summary>Computed commission entry per agent per payment/conversion.</summary>
public class CommissionEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AgentId { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? RuleId { get; set; }

    public decimal Amount { get; set; }
    public CommissionStatus Status { get; set; } = CommissionStatus.Pending;
    public string? Note { get; set; }
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public AppUser Agent { get; set; } = null!;
    public Payment? Payment { get; set; }
    public Lead? Lead { get; set; }
    public CommissionRule? Rule { get; set; }
}
