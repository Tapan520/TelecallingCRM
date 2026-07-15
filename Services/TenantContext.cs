using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Services;

/// <summary>
/// Holds the current tenant for the request lifetime.
/// </summary>
public class TenantContext
{
    public Tenant? Tenant { get; set; }
    public bool IsResolved { get; set; }

    public Guid TenantId => Tenant?.Id ?? Guid.Empty;
    public bool HasTenant => Tenant != null;
}
