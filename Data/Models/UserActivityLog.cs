namespace TelecallingCRM.Data.Models;

/// <summary>
/// System-wide user activity log. Records every significant action performed
/// by any user (admin, manager, agent) regardless of whether a lead is involved.
/// </summary>
public class UserActivityLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Short action name, e.g. "Login", "Logout", "PunchIn", "LeadCreated".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Module / area, e.g. "Auth", "Attendance", "Leads", "Calls".</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>Human-readable description of what happened.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Optional related entity id (lead, call, etc.).</summary>
    public Guid? EntityId { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}
