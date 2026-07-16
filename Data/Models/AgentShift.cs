namespace TelecallingCRM.Data.Models;

public enum DayOfWeekFlag
{
    Monday    = 1,
    Tuesday   = 2,
    Wednesday = 4,
    Thursday  = 8,
    Friday    = 16,
    Saturday  = 32,
    Sunday    = 64
}

/// <summary>Defines an agent's recurring weekly availability schedule.</summary>
public class AgentShift
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AgentId { get; set; }

    /// <summary>Bitmask of DayOfWeekFlag values (e.g. Mon+Tue = 3).</summary>
    public int WorkDays { get; set; } = 62; // Mon-Fri by default

    /// <summary>Start of shift in UTC (e.g. 03:30 = 09:00 IST).</summary>
    public TimeSpan ShiftStartUtc { get; set; } = new TimeSpan(3, 30, 0);

    /// <summary>End of shift in UTC (e.g. 13:30 = 19:00 IST).</summary>
    public TimeSpan ShiftEndUtc { get; set; } = new TimeSpan(13, 30, 0);

    public string? Timezone { get; set; } = "Asia/Kolkata";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public AppUser Agent { get; set; } = null!;
}

/// <summary>Records when an agent explicitly marks themselves online/offline.</summary>
public class AgentPresence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AgentId { get; set; }
    public bool IsOnline { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public AppUser Agent { get; set; } = null!;
}
