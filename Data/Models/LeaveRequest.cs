namespace TelecallingCRM.Data.Models;

public enum LeaveType { SickLeave, CasualLeave, EarnedLeave, UnpaidLeave, CompOff, Other }
public enum LeaveStatus { Pending, Approved, Rejected, Cancelled }

public class LeaveRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AgentId { get; set; }

    public LeaveType LeaveType { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    /// <summary>Number of working days requested.</summary>
    public int TotalDays { get; set; }

    public bool IsHalfDay { get; set; } = false;

    public string Reason { get; set; } = string.Empty;
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewerNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public AppUser Agent { get; set; } = null!;
    public AppUser? ReviewedBy { get; set; }
}
