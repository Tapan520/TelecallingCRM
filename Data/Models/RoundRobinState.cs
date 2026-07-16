namespace TelecallingCRM.Data.Models;

/// <summary>Round-robin assignment state per campaign (or tenant-wide when CampaignId is null).</summary>
public class RoundRobinState
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? CampaignId { get; set; }

    /// <summary>JSON-serialised ordered list of agent Guids.</summary>
    public string AgentQueueJson { get; set; } = "[]";

    /// <summary>Zero-based index of the next agent to assign.</summary>
    public int NextIndex { get; set; } = 0;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
}
