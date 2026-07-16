namespace TelecallingCRM.Data.Models;

/// <summary>A structured call script assigned to a campaign.</summary>
public class CallScript
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? CampaignId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty; // markdown or plain text
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public Campaign? Campaign { get; set; }
    public ICollection<CallDisposition> Dispositions { get; set; } = new List<CallDisposition>();
}

/// <summary>Disposition outcome options an agent selects after a call.</summary>
public class CallDisposition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? ScriptId { get; set; }

    public string Label { get; set; } = string.Empty;          // e.g. "Interested", "Callback"
    public string? Color { get; set; } = "#6366f1";            // hex colour for UI badge
    public bool ClosesLead { get; set; } = false;              // auto-mark lead as Dead/Converted?
    public LeadStatus? NextStatus { get; set; }                // maps to lead status
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public CallScript? Script { get; set; }
}
