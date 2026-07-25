namespace TelecallingCRM.Data.Models;

public enum ShiftSwapStatus { Pending, Approved, Rejected }
public enum WorkModeType { Office, WFH, Field }

public class ShiftSwapRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid RequestedById { get; set; }
    public Guid? SwapWithAgentId { get; set; }

    public DateTime SwapDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ShiftSwapStatus Status { get; set; } = ShiftSwapStatus.Pending;

    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewerNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public AppUser RequestedBy { get; set; } = null!;
    public AppUser? SwapWithAgent { get; set; }
    public AppUser? ReviewedBy { get; set; }
}

public class WorkModeLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AgentId { get; set; }
    public WorkModeType WorkMode { get; set; } = WorkModeType.Office;
    public DateTime Date { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public AppUser Agent { get; set; } = null!;
}
