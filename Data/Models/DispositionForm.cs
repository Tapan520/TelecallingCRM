namespace TelecallingCRM.Data.Models;

/// <summary>A post-call disposition form with custom fields filled by agents after a call.</summary>
public class DispositionForm
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? CampaignId { get; set; }

    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public Campaign? Campaign { get; set; }
    public ICollection<DispositionField> Fields { get; set; } = new List<DispositionField>();
    public ICollection<DispositionResponse> Responses { get; set; } = new List<DispositionResponse>();
}

public enum DispositionFieldType { Text, TextArea, Dropdown, Checkbox, Number, Date, Select, MultiSelect, Rating }

public class DispositionField
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FormId { get; set; }

    public string Label { get; set; } = string.Empty;
    public DispositionFieldType FieldType { get; set; } = DispositionFieldType.Text;
    /// <summary>Comma-separated options for Dropdown fields.</summary>
    public string? Options { get; set; }
    public bool IsRequired { get; set; } = false;
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DispositionForm Form { get; set; } = null!;
}

/// <summary>An agent's filled response for a call.</summary>
public class DispositionResponse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid FormId { get; set; }
    public Guid CallId { get; set; }
    public Guid AgentId { get; set; }
    public Guid LeadId { get; set; }

    /// <summary>JSON: { fieldId: value, ... }</summary>
    public string AnswersJson { get; set; } = "{}";
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public DispositionForm Form { get; set; } = null!;
    public Call Call { get; set; } = null!;
    public AppUser Agent { get; set; } = null!;
    public Lead Lead { get; set; } = null!;
}
