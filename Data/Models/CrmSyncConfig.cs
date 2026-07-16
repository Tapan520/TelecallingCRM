namespace TelecallingCRM.Data.Models;

/// <summary>Stores credentials and sync state for a third-party CRM integration (Salesforce / HubSpot).</summary>
public class CrmSyncConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }

    /// <summary>Provider name: "salesforce" | "hubspot"</summary>
    public string Provider { get; set; } = string.Empty;

    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? InstanceUrl { get; set; }    // Salesforce instance URL
    public string? PortalId { get; set; }       // HubSpot portal/hub ID
    public DateTime? TokenExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncedAt { get; set; }
    public string? LastSyncStatus { get; set; } // "ok" | error message

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
}

/// <summary>Log entry for each individual object synced to/from an external CRM.</summary>
public class CrmSyncLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CrmSyncConfigId { get; set; }

    public string Provider { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;  // "Lead", "Contact", "Deal"
    public string Direction { get; set; } = "push";         // "push" | "pull"
    public string ExternalId { get; set; } = string.Empty;
    public Guid? LocalLeadId { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;

    public CrmSyncConfig Config { get; set; } = null!;
}
