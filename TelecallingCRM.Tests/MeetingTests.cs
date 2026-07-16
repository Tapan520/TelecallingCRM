using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Tests;

/// <summary>Tests for #3 – Meeting model.</summary>
public class MeetingTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId1 = Guid.NewGuid();
    private readonly Guid _userId2 = Guid.NewGuid();
    private readonly Guid _leadId = Guid.NewGuid();

    public MeetingTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(opts);

        _db.Set<Tenant>().Add(new Tenant { Id = _tenantId, Name = "T3", Slug = "t3" });

        _db.Set<AppUser>().AddRange(
            new AppUser { Id = _userId1, TenantId = _tenantId, FullName = "Organiser", UserName = "u1", NormalizedUserName = "U1", Email = "u1@t.com", NormalizedEmail = "U1@T.COM", SecurityStamp = Guid.NewGuid().ToString() },
            new AppUser { Id = _userId2, TenantId = _tenantId, FullName = "Attendee", UserName = "u2", NormalizedUserName = "U2", Email = "u2@t.com", NormalizedEmail = "U2@T.COM", SecurityStamp = Guid.NewGuid().ToString() }
        );

        _db.Set<Lead>().Add(new Lead { Id = _leadId, TenantId = _tenantId, Name = "Lead Z", Phone = "7777777777" });

        _db.SaveChanges();
    }

    [Fact]
    public async Task CanScheduleMeeting()
    {
        var meeting = new Meeting
        {
            TenantId = _tenantId,
            LeadId = _leadId,
            OrganisedById = _userId1,
            Title = "Product Demo",
            Type = MeetingType.VideoCall,
            ScheduledAt = DateTime.UtcNow.AddDays(1),
            DurationMinutes = 30,
            Status = MeetingStatus.Scheduled
        };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        var saved = await _db.Meetings.SingleAsync(m => m.Title == "Product Demo");
        Assert.Equal(MeetingType.VideoCall, saved.Type);
        Assert.Equal(MeetingStatus.Scheduled, saved.Status);
        Assert.Equal(_tenantId, saved.TenantId);
    }

    [Fact]
    public async Task CanAddAttendeesToMeeting()
    {
        var meeting = new Meeting
        {
            TenantId = _tenantId, LeadId = _leadId, OrganisedById = _userId1,
            Title = "Site Visit", Type = MeetingType.SiteVisit,
            ScheduledAt = DateTime.UtcNow.AddDays(2), DurationMinutes = 60
        };
        _db.Meetings.Add(meeting);
        _db.MeetingAttendees.AddRange(
            new MeetingAttendee { Meeting = meeting, UserId = _userId1 },
            new MeetingAttendee { Meeting = meeting, UserId = _userId2 }
        );
        await _db.SaveChangesAsync();

        var attendees = await _db.MeetingAttendees
            .Where(a => a.MeetingId == meeting.Id)
            .ToListAsync();
        Assert.Equal(2, attendees.Count);
    }

    [Fact]
    public async Task CanCompleteMeetingWithOutcome()
    {
        var meeting = new Meeting
        {
            TenantId = _tenantId, LeadId = _leadId, OrganisedById = _userId1,
            Title = "Follow-up Call", Type = MeetingType.PhoneCall,
            ScheduledAt = DateTime.UtcNow.AddHours(-1), DurationMinutes = 15,
            Status = MeetingStatus.Scheduled
        };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        meeting.Status = MeetingStatus.Completed;
        meeting.Outcome = "Client agreed to purchase";
        await _db.SaveChangesAsync();

        var saved = await _db.Meetings.SingleAsync(m => m.Id == meeting.Id);
        Assert.Equal(MeetingStatus.Completed, saved.Status);
        Assert.Equal("Client agreed to purchase", saved.Outcome);
    }

    [Fact]
    public async Task CanCancelMeeting()
    {
        var meeting = new Meeting
        {
            TenantId = _tenantId, LeadId = _leadId, OrganisedById = _userId1,
            Title = "Intro Call", Type = MeetingType.PhoneCall,
            ScheduledAt = DateTime.UtcNow.AddDays(3), DurationMinutes = 20,
            Status = MeetingStatus.Scheduled
        };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        meeting.Status = MeetingStatus.Cancelled;
        await _db.SaveChangesAsync();

        var saved = await _db.Meetings.SingleAsync(m => m.Id == meeting.Id);
        Assert.Equal(MeetingStatus.Cancelled, saved.Status);
    }

    [Fact]
    public async Task CanMarkMeetingAsNoShow()
    {
        var meeting = new Meeting
        {
            TenantId = _tenantId, LeadId = _leadId, OrganisedById = _userId1,
            Title = "No-Show Meeting", Type = MeetingType.Demo,
            ScheduledAt = DateTime.UtcNow.AddHours(-2), DurationMinutes = 45,
            Status = MeetingStatus.Scheduled
        };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        meeting.Status = MeetingStatus.NoShow;
        await _db.SaveChangesAsync();

        var saved = await _db.Meetings.SingleAsync(m => m.Id == meeting.Id);
        Assert.Equal(MeetingStatus.NoShow, saved.Status);
    }

    [Fact]
    public async Task CanDeleteMeeting()
    {
        var meeting = new Meeting
        {
            TenantId = _tenantId, LeadId = _leadId, OrganisedById = _userId1,
            Title = "To Delete", Type = MeetingType.Other,
            ScheduledAt = DateTime.UtcNow.AddDays(7), DurationMinutes = 30
        };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        _db.Meetings.Remove(meeting);
        await _db.SaveChangesAsync();

        Assert.Equal(0, await _db.Meetings.CountAsync(m => m.Id == meeting.Id));
    }

    [Fact]
    public async Task Meeting_HasNavigationToLeadAndOrganiser()
    {
        var meeting = new Meeting
        {
            TenantId = _tenantId, LeadId = _leadId, OrganisedById = _userId1,
            Title = "Nav Test", Type = MeetingType.InPerson,
            ScheduledAt = DateTime.UtcNow.AddDays(1), DurationMinutes = 30
        };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        var loaded = await _db.Meetings
            .Include(m => m.Lead)
            .Include(m => m.OrganisedBy)
            .Include(m => m.Attendees)
            .SingleAsync(m => m.Id == meeting.Id);

        Assert.NotNull(loaded.Lead);
        Assert.NotNull(loaded.OrganisedBy);
        Assert.Equal("Lead Z", loaded.Lead.Name);
        Assert.Equal("Organiser", loaded.OrganisedBy.FullName);
    }

    [Fact]
    public async Task CanUpdateMeetingAgendaAndLink()
    {
        var meeting = new Meeting
        {
            TenantId = _tenantId, LeadId = _leadId, OrganisedById = _userId1,
            Title = "Update Test", Type = MeetingType.VideoCall,
            ScheduledAt = DateTime.UtcNow.AddDays(1), DurationMinutes = 30
        };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        meeting.Agenda = "Discuss pricing";
        meeting.MeetingLink = "https://meet.google.com/abc-xyz";
        await _db.SaveChangesAsync();

        var saved = await _db.Meetings.SingleAsync(m => m.Id == meeting.Id);
        Assert.Equal("Discuss pricing", saved.Agenda);
        Assert.Equal("https://meet.google.com/abc-xyz", saved.MeetingLink);
    }

    public void Dispose() => _db.Dispose();
}
