namespace TelecallingCRM.Data.Models;

public class ApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CreatedById { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;   // SHA-256 of the raw key
    public string KeyPrefix { get; set; } = string.Empty; // first 8 chars for display
    public string Scopes { get; set; } = "read";           // comma-separated: read, write, admin
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public AppUser CreatedBy { get; set; } = null!;
}
