namespace TelecallingCRM.Data.Models;

/// <summary>Tracks annual leave balance per agent per year.</summary>
public class LeaveBalance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AgentId { get; set; }
    public int Year { get; set; }

    public int SickLeaveTotal { get; set; } = 12;
    public int SickLeaveUsed { get; set; } = 0;

    public int CasualLeaveTotal { get; set; } = 12;
    public int CasualLeaveUsed { get; set; } = 0;

    public int EarnedLeaveTotal { get; set; } = 15;
    public int EarnedLeaveUsed { get; set; } = 0;

    public int CompOffTotal { get; set; } = 0;
    public int CompOffUsed { get; set; } = 0;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public AppUser Agent { get; set; } = null!;
}
