namespace TelecallingCRM.Data.Models;

public enum AnnouncementPriority { Normal, Important, Urgent }

public class Announcement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CreatedById { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Normal;

    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public AppUser CreatedBy { get; set; } = null!;
    public ICollection<AnnouncementRead> Reads { get; set; } = new List<AnnouncementRead>();
}

public class AnnouncementRead
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AnnouncementId { get; set; }
    public Guid UserId { get; set; }
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Announcement Announcement { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}
