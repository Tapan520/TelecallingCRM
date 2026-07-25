namespace TelecallingCRM.Data.Models;

public enum HolidayType { Public, Company, Optional }

public class Holiday
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public HolidayType Type { get; set; } = HolidayType.Public;
    public string? Description { get; set; }
    public bool IsRecurringYearly { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
}
