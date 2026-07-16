namespace TelecallingCRM.Data.Models;

/// <summary>
/// Reusable SMS template with variable placeholders like {{lead_name}}, {{agent_name}}.
/// </summary>
public class SmsTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;
    /// <summary>Template body. Supports {{lead_name}}, {{agent_name}}, {{phone}}, {{company}}.</summary>
    public string Body { get; set; } = string.Empty;
    public string? Category { get; set; }           // e.g. "followup", "promotion", "otp"
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
}

/// <summary>
/// WhatsApp Business API (WABA) approved template.
/// Pre-approved template names are used for outbound business-initiated messages.
/// </summary>
public class WhatsAppTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }

    /// <summary>Friendly internal name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Exact WABA-approved template name (used when calling the WABA API).</summary>
    public string TemplateName { get; set; } = string.Empty;
    /// <summary>BCP-47 language code, e.g. "en", "en_US", "hi".</summary>
    public string Language { get; set; } = "en";
    /// <summary>Preview / reference body with {{1}}, {{2}} placeholders.</summary>
    public string BodyPreview { get; set; } = string.Empty;
    /// <summary>Optional header text or media type (text/image/video/document).</summary>
    public string? HeaderType { get; set; }
    public string? HeaderValue { get; set; }
    /// <summary>Optional footer text.</summary>
    public string? Footer { get; set; }
    /// <summary>JSON array of button definitions (quick-reply / call-to-action).</summary>
    public string? ButtonsJson { get; set; }
    public string? Category { get; set; }           // e.g. "MARKETING", "UTILITY", "AUTHENTICATION"
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
}
