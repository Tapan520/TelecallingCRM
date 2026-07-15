namespace TelecallingCRM.Data.Models;

public enum MeetingStatus { Scheduled, Completed, Cancelled, NoShow }
public enum MeetingType { InPerson, PhoneCall, VideoCall, Demo, SiteVisit, Other }

public class Meeting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid OrganisedById { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Agenda { get; set; }
    public MeetingType Type { get; set; } = MeetingType.InPerson;
    public MeetingStatus Status { get; set; } = MeetingStatus.Scheduled;
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public string? Location { get; set; }         // physical address or meeting link
    public string? MeetingLink { get; set; }      // Google Meet / Zoom / Teams URL
    public string? Notes { get; set; }
    public string? Outcome { get; set; }          // post-meeting outcome summary
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public Lead Lead { get; set; } = null!;
    public AppUser OrganisedBy { get; set; } = null!;
    public ICollection<MeetingAttendee> Attendees { get; set; } = new List<MeetingAttendee>();
}

public class MeetingAttendee
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MeetingId { get; set; }
    public Guid UserId { get; set; }

    public Meeting Meeting { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}
