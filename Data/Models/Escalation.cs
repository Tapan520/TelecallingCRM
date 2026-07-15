namespace TelecallingCRM.Data.Models;

public enum EscalationStatus { Pending, Acknowledged, Resolved, Dismissed }

public enum EscalationTrigger
{
    MissedFollowUp,
    OverdueTask,
    NegativeSentiment,
    NoContactDays,
    HotLeadIgnored
}

/// <summary>Tenant-configurable rule that defines when and to whom leads are auto-escalated.</summary>
public class EscalationRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public EscalationTrigger Trigger { get; set; }
    /// <summary>Threshold value (days for NoContactDays; count for missed follow-ups, etc.).</summary>
    public int ThresholdValue { get; set; } = 3;
    /// <summary>Manager / admin user who receives the escalation notification.</summary>
    public Guid EscalateToId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public AppUser EscalateTo { get; set; } = null!;
    public ICollection<Escalation> Escalations { get; set; } = new List<Escalation>();
}

/// <summary>Individual escalation instance raised for a specific lead.</summary>
public class Escalation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    /// <summary>Original agent who owns the lead.</summary>
    public Guid AssignedAgentId { get; set; }
    /// <summary>Manager / admin to whom the escalation is directed.</summary>
    public Guid EscalatedToId { get; set; }
    public Guid? RuleId { get; set; }
    public EscalationStatus Status { get; set; } = EscalationStatus.Pending;
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Lead Lead { get; set; } = null!;
    public AppUser AssignedAgent { get; set; } = null!;
    public AppUser EscalatedTo { get; set; } = null!;
    public EscalationRule? Rule { get; set; }
}
