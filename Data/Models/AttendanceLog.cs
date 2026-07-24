namespace TelecallingCRM.Data.Models;

public enum AttendanceStatus { Present, Absent, HalfDay, Late }

/// <summary>
/// Records a single punch-in / punch-out session for an agent.
/// Admin/manager can create or edit entries on behalf of agents.
/// </summary>
public class AttendanceLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AgentId { get; set; }

    public DateTime PunchIn { get; set; }
    public DateTime? PunchOut { get; set; }

    /// <summary>Computed on punch-out: total minutes worked.</summary>
    public int WorkMinutes { get; set; } = 0;

    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

    /// <summary>Optional note (e.g. reason for late arrival or manual correction reason).</summary>
    public string? Notes { get; set; }

    /// <summary>True when an admin/manager created or edited this entry on behalf of the agent.</summary>
    public bool IsManualEntry { get; set; } = false;

    /// <summary>The user who performed the punch-in action (may differ from AgentId for manual entries).</summary>
    public Guid PunchedInById { get; set; }

    /// <summary>The user who performed the punch-out action (may differ from AgentId for manual entries).</summary>
    public Guid? PunchedOutById { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public AppUser Agent { get; set; } = null!;
    public AppUser PunchedInBy { get; set; } = null!;
    public AppUser? PunchedOutBy { get; set; }
}
