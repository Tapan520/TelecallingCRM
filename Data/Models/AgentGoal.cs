namespace TelecallingCRM.Data.Models;

/// <summary>
/// Performance goal set by an admin/manager for an agent over a specific period.
/// Actual vs. target metrics are computed at query time from Calls and Leads.
/// </summary>
public class AgentGoal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AgentId { get; set; }
    public Guid CreatedById { get; set; }

    /// <summary>Human-readable label, e.g. "July 2026 Daily Target".</summary>
    public string Label { get; set; } = string.Empty;

    // ?? Target values ?????????????????????????????????????????????????????
    public int TargetCalls { get; set; } = 0;
    public int TargetConversions { get; set; } = 0;
    /// <summary>Target cumulative talk time in seconds.</summary>
    public int TargetTalkSeconds { get; set; } = 0;
    /// <summary>Target follow-ups completed within the period.</summary>
    public int TargetFollowUps { get; set; } = 0;

    // ?? Period ????????????????????????????????????????????????????????????
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public AppUser Agent { get; set; } = null!;
    public AppUser CreatedBy { get; set; } = null!;
}
