namespace TelecallingCRM.Data.Models;

/// <summary>
/// A phone number on the Do-Not-Call list for a tenant.
/// Calls to DNC numbers are blocked at the API level.
/// </summary>
public class DncEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }

    /// <summary>Normalised phone number (digits only, e.g. "9876543210").</summary>
    public string Phone { get; set; } = string.Empty;

    public string? Reason { get; set; }
    public Guid AddedById { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public AppUser AddedBy { get; set; } = null!;
}
