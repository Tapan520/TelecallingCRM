namespace TelecallingCRM.Data.Models;

public enum BadgeType
{
    FirstCall, HundredCalls, FirstSale, TopPerformerWeek,
    TopPerformerMonth, PerfectAttendance, FastResponder, LeadConvertor
}

public class AgentBadge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AgentId { get; set; }

    public BadgeType Badge { get; set; }
    public string? Notes { get; set; }
    public int Points { get; set; } = 0;

    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public AppUser Agent { get; set; } = null!;
}
