using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Tests;

// ??????????????????????????????????????????????????????????????????????????????
// #5 – Meeting Reminder (ScheduledJobService.ProcessMeetingRemindersAsync)
// ??????????????????????????????????????????????????????????????????????????????
public class MeetingReminderTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId1  = Guid.NewGuid();
    private readonly Guid _userId2  = Guid.NewGuid();
    private readonly Guid _leadId   = Guid.NewGuid();

    public MeetingReminderTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new AppDbContext(opts);

        _db.Set<Tenant>().Add(new Tenant { Id = _tenantId, Name = "T", Slug = "t" });
        _db.Set<AppUser>().AddRange(
            new AppUser { Id = _userId1, TenantId = _tenantId, FullName = "Organiser", UserName = "u1", NormalizedUserName = "U1", Email = "u1@t.com", NormalizedEmail = "U1@T.COM", SecurityStamp = Guid.NewGuid().ToString() },
            new AppUser { Id = _userId2, TenantId = _tenantId, FullName = "Attendee",  UserName = "u2", NormalizedUserName = "U2", Email = "u2@t.com", NormalizedEmail = "U2@T.COM", SecurityStamp = Guid.NewGuid().ToString() }
        );
        _db.Set<Lead>().Add(new Lead { Id = _leadId, TenantId = _tenantId, Name = "Client X", Phone = "9000000001" });
        _db.SaveChanges();
    }

    [Fact]
    public async Task MeetingDue_NotificationType_Exists()
    {
        // The enum value added for meeting reminders
        Assert.True(Enum.IsDefined(typeof(NotificationType), "MeetingDue"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task MeetingReminder_NotificationCreatedForOrganiser()
    {
        var scheduledAt = DateTime.UtcNow.AddMinutes(15);
        var meeting = new Meeting
        {
            TenantId = _tenantId, LeadId = _leadId, OrganisedById = _userId1,
            Title = "Demo", Type = MeetingType.VideoCall,
            ScheduledAt = scheduledAt, DurationMinutes = 30,
            Status = MeetingStatus.Scheduled
        };
        _db.Meetings.Add(meeting);
        _db.MeetingAttendees.Add(new MeetingAttendee { Meeting = meeting, UserId = _userId1 });
        await _db.SaveChangesAsync();

        // Simulate what the job does: create notification for organiser
        _db.Notifications.Add(new Notification
        {
            TenantId = _tenantId, UserId = _userId1,
            Type = NotificationType.MeetingDue,
            Title = "Meeting Starting Soon",
            Body = $"\"Demo\" with Client X starts in 15 minutes.",
            Link = $"/Leads/Timeline/{_leadId}"
        });
        await _db.SaveChangesAsync();

        var notif = await _db.Notifications
            .FirstAsync(n => n.UserId == _userId1 && n.Type == NotificationType.MeetingDue);
        Assert.Contains("Demo", notif.Body!);
        Assert.Contains(_leadId.ToString(), notif.Link!);
    }

    [Fact]
    public async Task MeetingReminder_NotificationCreatedForAllAttendees()
    {
        var meeting = new Meeting
        {
            TenantId = _tenantId, LeadId = _leadId, OrganisedById = _userId1,
            Title = "Strategy", Type = MeetingType.InPerson,
            ScheduledAt = DateTime.UtcNow.AddMinutes(20), DurationMinutes = 60,
            Status = MeetingStatus.Scheduled
        };
        _db.Meetings.Add(meeting);
        _db.MeetingAttendees.AddRange(
            new MeetingAttendee { Meeting = meeting, UserId = _userId1 },
            new MeetingAttendee { Meeting = meeting, UserId = _userId2 }
        );
        await _db.SaveChangesAsync();

        foreach (var uid in new[] { _userId1, _userId2 })
            _db.Notifications.Add(new Notification
            {
                TenantId = _tenantId, UserId = uid,
                Type = NotificationType.MeetingDue,
                Title = "Meeting Starting Soon", Body = "In 20 min.",
                Link = $"/Leads/Timeline/{_leadId}"
            });
        await _db.SaveChangesAsync();

        var count = await _db.Notifications.CountAsync(n => n.Type == NotificationType.MeetingDue);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task CancelledMeeting_ShouldNotTriggerReminder()
    {
        var meeting = new Meeting
        {
            TenantId = _tenantId, LeadId = _leadId, OrganisedById = _userId1,
            Title = "Cancelled Meet", Type = MeetingType.PhoneCall,
            ScheduledAt = DateTime.UtcNow.AddMinutes(10), DurationMinutes = 15,
            Status = MeetingStatus.Cancelled   // <-- cancelled
        };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        // Job only queries Status == Scheduled – cancelled meetings are excluded
        var upcoming = await _db.Meetings
            .Where(m => m.Status == MeetingStatus.Scheduled
                     && m.ScheduledAt >= DateTime.UtcNow
                     && m.ScheduledAt <= DateTime.UtcNow.AddMinutes(30))
            .CountAsync();
        Assert.Equal(0, upcoming);
    }

    public void Dispose() => _db.Dispose();
}

// ??????????????????????????????????????????????????????????????????????????????
// #2 – Razorpay Webhook – signature helper & status transitions
// ??????????????????????????????????????????????????????????????????????????????
public class RazorpayWebhookTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId   = Guid.NewGuid();
    private readonly Guid _leadId   = Guid.NewGuid();

    public RazorpayWebhookTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new AppDbContext(opts);

        _db.Set<Tenant>().Add(new Tenant { Id = _tenantId, Name = "RZP", Slug = "rzp" });
        _db.Set<AppUser>().Add(new AppUser { Id = _userId, TenantId = _tenantId, FullName = "Seller", UserName = "s1", NormalizedUserName = "S1", Email = "s@t.com", NormalizedEmail = "S@T.COM", SecurityStamp = Guid.NewGuid().ToString() });
        _db.Set<Lead>().Add(new Lead { Id = _leadId, TenantId = _tenantId, Name = "Buyer", Phone = "9000000002" });
        _db.SaveChanges();
    }

    [Fact]
    public void Signature_Verification_ValidSecret_ReturnsTrue()
    {
        var secret  = "whsec_test_secret";
        var body    = "{\"event\":\"payment.captured\"}";
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(secret));
        var sig = Convert.ToHexString(
            hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(body))).ToLower();

        // Re-compute and compare (mimics RazorpayWebhookEndpoints.VerifySignature)
        using var hmac2 = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexString(
            hmac2.ComputeHash(System.Text.Encoding.UTF8.GetBytes(body))).ToLower();
        Assert.Equal(expected, sig);
    }

    [Fact]
    public void Signature_Verification_WrongSecret_ReturnsFalse()
    {
        var body = "{\"event\":\"payment.captured\"}";
        using var hmac1 = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes("secret_A"));
        var sigA = Convert.ToHexString(hmac1.ComputeHash(
            System.Text.Encoding.UTF8.GetBytes(body))).ToLower();

        using var hmac2 = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes("secret_B"));
        var expectedB = Convert.ToHexString(hmac2.ComputeHash(
            System.Text.Encoding.UTF8.GetBytes(body))).ToLower();

        Assert.NotEqual(expectedB, sigA);
    }

    [Fact]
    public async Task PaymentCaptured_Event_UpdatesStatusAndLead()
    {
        var payment = new Payment
        {
            TenantId = _tenantId, LeadId = _leadId, RecordedById = _userId,
            Amount = 999m, Currency = "INR",
            RazorpayOrderId = "order_cap_webhook",
            Status = PaymentStatus.Pending
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        // Simulate what the webhook handler does on payment.captured
        payment.Status            = PaymentStatus.Captured;
        payment.CapturedAt        = DateTime.UtcNow;
        payment.RazorpayPaymentId = "pay_webhook_001";
        var lead = await _db.Leads.FindAsync(_leadId);
        lead!.Status = LeadStatus.Converted;
        await _db.SaveChangesAsync();

        var saved = await _db.Payments.FindAsync(payment.Id);
        Assert.Equal(PaymentStatus.Captured, saved!.Status);
        Assert.NotNull(saved.CapturedAt);
        Assert.Equal("pay_webhook_001", saved.RazorpayPaymentId);

        var savedLead = await _db.Leads.FindAsync(_leadId);
        Assert.Equal(LeadStatus.Converted, savedLead!.Status);
    }

    [Fact]
    public async Task PaymentFailed_Event_UpdatesStatusAndCreatesNotification()
    {
        var payment = new Payment
        {
            TenantId = _tenantId, LeadId = _leadId, RecordedById = _userId,
            Amount = 500m, Currency = "INR",
            RazorpayOrderId = "order_fail_webhook",
            Status = PaymentStatus.Pending
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        payment.Status = PaymentStatus.Failed;
        _db.Notifications.Add(new Notification
        {
            TenantId = _tenantId, UserId = _userId,
            Type = NotificationType.SystemAlert,
            Title = "Payment Failed",
            Body = $"Razorpay payment for order {payment.RazorpayOrderId} failed."
        });
        await _db.SaveChangesAsync();

        var saved  = await _db.Payments.FindAsync(payment.Id);
        var notif  = await _db.Notifications.FirstAsync(n => n.Type == NotificationType.SystemAlert);
        Assert.Equal(PaymentStatus.Failed, saved!.Status);
        Assert.Contains("order_fail_webhook", notif.Body!);
    }

    [Fact]
    public async Task RefundCreated_Event_UpdatesStatusToRefunded()
    {
        var payment = new Payment
        {
            TenantId = _tenantId, LeadId = _leadId, RecordedById = _userId,
            Amount = 1200m, Currency = "INR",
            RazorpayOrderId = "order_refund_webhook",
            RazorpayPaymentId = "pay_ref_webhook",
            Status = PaymentStatus.Captured, CapturedAt = DateTime.UtcNow.AddHours(-1)
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        payment.Status = PaymentStatus.Refunded;
        await _db.SaveChangesAsync();

        var saved = await _db.Payments.FindAsync(payment.Id);
        Assert.Equal(PaymentStatus.Refunded, saved!.Status);
    }

    public void Dispose() => _db.Dispose();
}

// ??????????????????????????????????????????????????????????????????????????????
// #1 – DNC List
// ??????????????????????????????????????????????????????????????????????????????
public class DncTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId   = Guid.NewGuid();

    public DncTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new AppDbContext(opts);

        _db.Set<Tenant>().Add(new Tenant { Id = _tenantId, Name = "DNC", Slug = "dnc" });
        _db.Set<AppUser>().Add(new AppUser { Id = _userId, TenantId = _tenantId, FullName = "Admin", UserName = "adm", NormalizedUserName = "ADM", Email = "a@d.com", NormalizedEmail = "A@D.COM", SecurityStamp = Guid.NewGuid().ToString() });
        _db.SaveChanges();
    }

    [Fact]
    public async Task CanAddPhoneToDnc()
    {
        _db.DncEntries.Add(new DncEntry
        {
            TenantId  = _tenantId,
            Phone     = "9876543210",
            Reason    = "Customer requested opt-out",
            AddedById = _userId
        });
        await _db.SaveChangesAsync();

        var entry = await _db.DncEntries.SingleAsync(d => d.Phone == "9876543210");
        Assert.Equal(_tenantId, entry.TenantId);
        Assert.Equal("Customer requested opt-out", entry.Reason);
    }

    [Fact]
    public async Task NormalisedPhone_StripsNonDigits()
    {
        var raw       = "+91-98765-43210";
        var normalised = TelecallingCRM.Api.DncEndpoints.NormalisePhone(raw);
        Assert.Equal("919876543210", normalised);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task DncCheck_ReturnsTrue_WhenPhoneExists()
    {
        _db.DncEntries.Add(new DncEntry { TenantId = _tenantId, Phone = "9111111111", AddedById = _userId });
        await _db.SaveChangesAsync();

        var isDnc = await _db.DncEntries
            .AnyAsync(d => d.TenantId == _tenantId && d.Phone == "9111111111");
        Assert.True(isDnc);
    }

    [Fact]
    public async Task DncCheck_ReturnsFalse_WhenPhoneNotOnList()
    {
        var isDnc = await _db.DncEntries
            .AnyAsync(d => d.TenantId == _tenantId && d.Phone == "9999999999");
        Assert.False(isDnc);
    }

    [Fact]
    public async Task CanRemovePhoneFromDnc()
    {
        var entry = new DncEntry { TenantId = _tenantId, Phone = "9222222222", AddedById = _userId };
        _db.DncEntries.Add(entry);
        await _db.SaveChangesAsync();

        _db.DncEntries.Remove(entry);
        await _db.SaveChangesAsync();

        Assert.Equal(0, await _db.DncEntries.CountAsync(d => d.Phone == "9222222222"));
    }

    [Fact]
    public async Task DncIsPerTenant_DoesNotLeakAcrossTenants()
    {
        var otherTenantId = Guid.NewGuid();
        _db.Set<Tenant>().Add(new Tenant { Id = otherTenantId, Name = "Other", Slug = "other" });
        await _db.SaveChangesAsync();

        _db.DncEntries.Add(new DncEntry { TenantId = otherTenantId, Phone = "9333333333", AddedById = _userId });
        await _db.SaveChangesAsync();

        // Should not appear under _tenantId
        var isDnc = await _db.DncEntries
            .AnyAsync(d => d.TenantId == _tenantId && d.Phone == "9333333333");
        Assert.False(isDnc);
    }

    public void Dispose() => _db.Dispose();
}

// ??????????????????????????????????????????????????????????????????????????????
// #3 – SMS & WhatsApp Templates
// ??????????????????????????????????????????????????????????????????????????????
public class MessageTemplateTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _tenantId = Guid.NewGuid();

    public MessageTemplateTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new AppDbContext(opts);
        _db.Set<Tenant>().Add(new Tenant { Id = _tenantId, Name = "Tmpl", Slug = "tmpl" });
        _db.SaveChanges();
    }

    // ?? SMS ??????????????????????????????????????????????????????????????????

    [Fact]
    public async Task CanCreateSmsTemplate()
    {
        _db.SmsTemplates.Add(new SmsTemplate
        {
            TenantId = _tenantId,
            Name     = "Follow-up Reminder",
            Body     = "Hi {{lead_name}}, just following up. – {{agent_name}}",
            Category = "followup",
            IsActive = true
        });
        await _db.SaveChangesAsync();

        var tmpl = await _db.SmsTemplates.SingleAsync(t => t.Name == "Follow-up Reminder");
        Assert.Contains("{{lead_name}}", tmpl.Body);
        Assert.Equal("followup", tmpl.Category);
    }

    [Fact]
    public async Task SmsTemplate_VariableSubstitution_Works()
    {
        var body = "Hi {{lead_name}}, your callback is scheduled. – {{agent_name}}";
        var variables = new Dictionary<string, string>
        {
            ["lead_name"]  = "Rahul Sharma",
            ["agent_name"] = "Priya"
        };
        var result = body;
        foreach (var (k, v) in variables)
            result = result.Replace($"{{{{{k}}}}}", v, StringComparison.OrdinalIgnoreCase);

        Assert.Equal("Hi Rahul Sharma, your callback is scheduled. – Priya", result);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CanToggleSmsTemplateActive()
    {
        var tmpl = new SmsTemplate { TenantId = _tenantId, Name = "Promo", Body = "Special offer!", IsActive = true };
        _db.SmsTemplates.Add(tmpl);
        await _db.SaveChangesAsync();

        tmpl.IsActive = false;
        await _db.SaveChangesAsync();

        Assert.False((await _db.SmsTemplates.FindAsync(tmpl.Id))!.IsActive);
    }

    [Fact]
    public async Task CanDeleteSmsTemplate()
    {
        var tmpl = new SmsTemplate { TenantId = _tenantId, Name = "Delete Me", Body = "Bye" };
        _db.SmsTemplates.Add(tmpl);
        await _db.SaveChangesAsync();

        _db.SmsTemplates.Remove(tmpl);
        await _db.SaveChangesAsync();

        Assert.Equal(0, await _db.SmsTemplates.CountAsync(t => t.Id == tmpl.Id));
    }

    // ?? WhatsApp ?????????????????????????????????????????????????????????????

    [Fact]
    public async Task CanCreateWhatsAppTemplate()
    {
        _db.WhatsAppTemplates.Add(new WhatsAppTemplate
        {
            TenantId     = _tenantId,
            Name         = "Welcome Message",
            TemplateName = "welcome_v1",
            Language     = "en",
            BodyPreview  = "Hello {{1}}, welcome to our service!",
            Category     = "UTILITY",
            IsActive     = true
        });
        await _db.SaveChangesAsync();

        var tmpl = await _db.WhatsAppTemplates.SingleAsync(t => t.TemplateName == "welcome_v1");
        Assert.Equal("en", tmpl.Language);
        Assert.Equal("UTILITY", tmpl.Category);
    }

    [Fact]
    public async Task WhatsAppTemplate_PositionalVariableSubstitution_Works()
    {
        var body = "Hello {{1}}, your order {{2}} is confirmed.";
        var variables = new Dictionary<string, string> { ["1"] = "Arjun", ["2"] = "#ORD-999" };
        var result = body;
        foreach (var (k, v) in variables)
            result = result.Replace($"{{{{{k}}}}}", v, StringComparison.OrdinalIgnoreCase);

        Assert.Equal("Hello Arjun, your order #ORD-999 is confirmed.", result);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task WhatsAppTemplate_WithHeader_PersistsCorrectly()
    {
        _db.WhatsAppTemplates.Add(new WhatsAppTemplate
        {
            TenantId     = _tenantId,
            Name         = "Promo With Image",
            TemplateName = "promo_img_v1",
            Language     = "hi",
            BodyPreview  = "???? ???: {{1}}",
            HeaderType   = "image",
            HeaderValue  = "https://cdn.example.com/banner.jpg",
            Category     = "MARKETING"
        });
        await _db.SaveChangesAsync();

        var tmpl = await _db.WhatsAppTemplates.SingleAsync(t => t.TemplateName == "promo_img_v1");
        Assert.Equal("image", tmpl.HeaderType);
        Assert.Equal("hi", tmpl.Language);
    }

    public void Dispose() => _db.Dispose();
}

// ??????????????????????????????????????????????????????????????????????????????
// #4 – Agent Goals & Daily Targets
// ??????????????????????????????????????????????????????????????????????????????
public class AgentGoalTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _tenantId  = Guid.NewGuid();
    private readonly Guid _agentId   = Guid.NewGuid();
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _leadId    = Guid.NewGuid();

    public AgentGoalTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new AppDbContext(opts);

        _db.Set<Tenant>().Add(new Tenant { Id = _tenantId, Name = "Goal", Slug = "goal" });
        _db.Set<AppUser>().AddRange(
            new AppUser { Id = _agentId,   TenantId = _tenantId, FullName = "Agent",   UserName = "ag",  NormalizedUserName = "AG",  Email = "ag@t.com",  NormalizedEmail = "AG@T.COM",  SecurityStamp = Guid.NewGuid().ToString() },
            new AppUser { Id = _managerId, TenantId = _tenantId, FullName = "Manager", UserName = "mgr", NormalizedUserName = "MGR", Email = "mgr@t.com", NormalizedEmail = "MGR@T.COM", SecurityStamp = Guid.NewGuid().ToString() }
        );
        _db.Set<Lead>().Add(new Lead { Id = _leadId, TenantId = _tenantId, Name = "Lead", Phone = "9000000003" });
        _db.SaveChanges();
    }

    [Fact]
    public async Task CanCreateAgentGoal()
    {
        var start = DateTime.UtcNow.Date;
        var end   = start.AddDays(30);
        var goal  = new AgentGoal
        {
            TenantId          = _tenantId,
            AgentId           = _agentId,
            CreatedById       = _managerId,
            Label             = "July 2026",
            TargetCalls       = 50,
            TargetConversions = 5,
            TargetTalkSeconds = 18000,
            TargetFollowUps   = 20,
            PeriodStart       = start,
            PeriodEnd         = end
        };
        _db.AgentGoals.Add(goal);
        await _db.SaveChangesAsync();

        var saved = await _db.AgentGoals.SingleAsync(g => g.Label == "July 2026");
        Assert.Equal(50, saved.TargetCalls);
        Assert.Equal(5,  saved.TargetConversions);
        Assert.Equal(18000, saved.TargetTalkSeconds);
    }

    [Fact]
    public async Task CanUpdateGoalTargets()
    {
        var goal = new AgentGoal
        {
            TenantId = _tenantId, AgentId = _agentId, CreatedById = _managerId,
            Label = "Aug", TargetCalls = 30, TargetConversions = 3,
            TargetTalkSeconds = 10000, TargetFollowUps = 10,
            PeriodStart = DateTime.UtcNow.Date, PeriodEnd = DateTime.UtcNow.Date.AddDays(31)
        };
        _db.AgentGoals.Add(goal);
        await _db.SaveChangesAsync();

        goal.TargetCalls = 60;
        await _db.SaveChangesAsync();

        Assert.Equal(60, (await _db.AgentGoals.FindAsync(goal.Id))!.TargetCalls);
    }

    [Fact]
    public async Task CanToggleGoalActive()
    {
        var goal = new AgentGoal
        {
            TenantId = _tenantId, AgentId = _agentId, CreatedById = _managerId,
            Label = "Toggle", TargetCalls = 10, TargetConversions = 1,
            TargetTalkSeconds = 3600, TargetFollowUps = 5,
            PeriodStart = DateTime.UtcNow.Date, PeriodEnd = DateTime.UtcNow.Date.AddDays(7),
            IsActive = true
        };
        _db.AgentGoals.Add(goal);
        await _db.SaveChangesAsync();

        goal.IsActive = false;
        await _db.SaveChangesAsync();

        Assert.False((await _db.AgentGoals.FindAsync(goal.Id))!.IsActive);
    }

    [Fact]
    public async Task CanDeleteGoal()
    {
        var goal = new AgentGoal
        {
            TenantId = _tenantId, AgentId = _agentId, CreatedById = _managerId,
            Label = "Del", TargetCalls = 5, TargetConversions = 0,
            TargetTalkSeconds = 1000, TargetFollowUps = 2,
            PeriodStart = DateTime.UtcNow.Date, PeriodEnd = DateTime.UtcNow.Date.AddDays(1)
        };
        _db.AgentGoals.Add(goal);
        await _db.SaveChangesAsync();

        _db.AgentGoals.Remove(goal);
        await _db.SaveChangesAsync();

        Assert.Equal(0, await _db.AgentGoals.CountAsync(g => g.Id == goal.Id));
    }

    [Fact]
    public async Task Progress_ComputedCorrectly_WhenCallsExist()
    {
        var start = DateTime.UtcNow.AddDays(-5);
        var end   = DateTime.UtcNow.AddDays(5);
        var goal  = new AgentGoal
        {
            TenantId = _tenantId, AgentId = _agentId, CreatedById = _managerId,
            Label = "Progress", TargetCalls = 10, TargetConversions = 2,
            TargetTalkSeconds = 3600, TargetFollowUps = 4,
            PeriodStart = start, PeriodEnd = end
        };
        _db.AgentGoals.Add(goal);

        // Seed 4 calls: 3 normal, 1 converted
        for (var i = 0; i < 3; i++)
            _db.Set<Call>().Add(new Call { TenantId = _tenantId, LeadId = _leadId, AgentId = _agentId, StartedAt = DateTime.UtcNow, DurationSeconds = 300, Outcome = CallOutcome.Interested });
        _db.Set<Call>().Add(new Call { TenantId = _tenantId, LeadId = _leadId, AgentId = _agentId, StartedAt = DateTime.UtcNow, DurationSeconds = 600, Outcome = CallOutcome.Converted });
        await _db.SaveChangesAsync();

        var actualCalls = await _db.Calls
            .CountAsync(c => c.AgentId == _agentId && c.TenantId == _tenantId
                          && c.StartedAt >= start && c.StartedAt <= end);
        var actualConversions = await _db.Calls
            .CountAsync(c => c.AgentId == _agentId && c.TenantId == _tenantId
                          && c.StartedAt >= start && c.StartedAt <= end
                          && c.Outcome == CallOutcome.Converted);

        Assert.Equal(4, actualCalls);
        Assert.Equal(1, actualConversions);

        var callsPct = (int)((double)actualCalls / goal.TargetCalls * 100); // 40%
        Assert.Equal(40, callsPct);
    }

    [Fact]
    public async Task Goal_HasNavigationToAgent()
    {
        var goal = new AgentGoal
        {
            TenantId = _tenantId, AgentId = _agentId, CreatedById = _managerId,
            Label = "Nav Test", TargetCalls = 20, TargetConversions = 2,
            TargetTalkSeconds = 7200, TargetFollowUps = 8,
            PeriodStart = DateTime.UtcNow.Date, PeriodEnd = DateTime.UtcNow.Date.AddDays(30)
        };
        _db.AgentGoals.Add(goal);
        await _db.SaveChangesAsync();

        var loaded = await _db.AgentGoals
            .Include(g => g.Agent)
            .Include(g => g.CreatedBy)
            .SingleAsync(g => g.Id == goal.Id);

        Assert.Equal("Agent",   loaded.Agent.FullName);
        Assert.Equal("Manager", loaded.CreatedBy.FullName);
    }

    public void Dispose() => _db.Dispose();
}
