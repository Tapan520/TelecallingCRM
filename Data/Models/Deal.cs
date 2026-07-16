namespace TelecallingCRM.Data.Models;

public enum DealStage
{
    Prospecting,
    Qualification,
    Proposal,
    Negotiation,
    ClosedWon,
    ClosedLost
}

public class Deal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid? AssignedToId { get; set; }

    public string Title { get; set; } = string.Empty;
    public decimal Value { get; set; } = 0;
    public string Currency { get; set; } = "INR";
    public DealStage Stage { get; set; } = DealStage.Prospecting;
    public int Probability { get; set; } = 10; // 0-100 %
    public DateTime? ExpectedCloseDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public Lead Lead { get; set; } = null!;
    public AppUser? AssignedTo { get; set; }
}
