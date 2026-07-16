using Hangfire;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using EscalationStatus = TelecallingCRM.Data.Models.EscalationStatus;

namespace TelecallingCRM.Services;

/// <summary>
/// Hangfire recurring jobs:
///  - ProcessDueItemsAsync      every 5 min  – overdue tasks, due follow-up/task notifications, auto-escalation
///  - ProcessMeetingRemindersAsync every 5 min – meeting start reminders
///  - AutoTranscribeCallsAsync  every 10 min – queue Whisper transcription for recorded calls without transcripts
///  - RescoreLeadsAsync         every 2 hrs  – re-score leads whose calls were updated recently
///  - SendDailyDigestAsync      daily 08:00  – send yesterday's summary email to admins/managers
/// All notification writes now go through INotificationSender which also pushes a SignalR event.
/// </summary>
public class ScheduledJobService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ScheduledJobService> _logger;
    private readonly IWebhookDispatcher _webhookDispatcher;
    private readonly INotificationSender _notifSender;
    private readonly IOpenRouterService _ai;
    private readonly IMessageDispatcher _messageDispatcher;

    public ScheduledJobService(
        AppDbContext db,
        ILogger<ScheduledJobService> logger,
        IWebhookDispatcher webhookDispatcher,
        INotificationSender notifSender,
        IOpenRouterService ai,
        IMessageDispatcher messageDispatcher)
    {
        _db = db;
        _logger = logger;
        _webhookDispatcher = webhookDispatcher;
        _notifSender = notifSender;
        _ai = ai;
        _messageDispatcher = messageDispatcher;
    }

    // ?????????????????????????????????????????????????????????????????????????
    // 1. Process due items (every 5 minutes)
    // ?????????????????????????????????????????????????????????????????????????
    [AutomaticRetry(Attempts = 2)]
    public async Task ProcessDueItemsAsync()
    {
        var now = DateTime.UtcNow;
        var notifWindow = now.AddMinutes(30);

        // 1a. Mark overdue tasks
        var overdue = await _db.Tasks
            .Where(t => t.Status == TelecallingCRM.Data.Models.TaskStatus.Pending && t.DueAt < now)
            .ToListAsync();
        foreach (var t in overdue)
            t.Status = TelecallingCRM.Data.Models.TaskStatus.Overdue;

        // 1b. Notify for tasks due within 30 minutes (real-time push via SignalR)
        var tasksDue = await _db.Tasks
            .Where(t => t.Status == TelecallingCRM.Data.Models.TaskStatus.Pending
                     && t.DueAt >= now && t.DueAt <= notifWindow)
            .ToListAsync();

        var taskDueNotifs = new List<Notification>();
        foreach (var t in tasksDue)
        {
            var already = await _db.Notifications.AnyAsync(n =>
                n.UserId == t.AssignedToId &&
                n.Type == NotificationType.TaskDue &&
                n.Link != null && n.Link.Contains(t.Id.ToString()) &&
                n.CreatedAt >= now.AddMinutes(-31));
            if (!already)
                taskDueNotifs.Add(new Notification
                {
                    TenantId = t.TenantId, UserId = t.AssignedToId,
                    Type = NotificationType.TaskDue,
                    Title = "Task Due Soon",
                    Body = $"\"{t.Title}\" is due in {(int)(t.DueAt - now).TotalMinutes} minutes.",
                    Link = "/Tasks"
                });
        }
        if (taskDueNotifs.Count > 0) await _notifSender.SendManyAsync(taskDueNotifs);

        // 1c. Notify for follow-ups due within 30 minutes
        var followUpsDue = await _db.FollowUps
            .Where(f => f.Status == FollowUpStatus.Pending
                     && f.ScheduledAt >= now && f.ScheduledAt <= notifWindow)
            .Include(f => f.Lead)
            .ToListAsync();

        var followUpNotifs = new List<Notification>();
        foreach (var f in followUpsDue)
        {
            var already = await _db.Notifications.AnyAsync(n =>
                n.UserId == f.AssignedToId &&
                n.Type == NotificationType.FollowUpDue &&
                n.Link != null && n.Link.Contains(f.LeadId.ToString()) &&
                n.CreatedAt >= now.AddMinutes(-31));
            if (!already)
                followUpNotifs.Add(new Notification
                {
                    TenantId = f.TenantId, UserId = f.AssignedToId,
                    Type = NotificationType.FollowUpDue,
                    Title = "Follow-up Due Soon",
                    Body = $"Follow-up with {f.Lead.Name} via {f.Channel} in {(int)(f.ScheduledAt - now).TotalMinutes} minutes.",
                    Link = $"/Leads/Timeline/{f.LeadId}"
                });
        }
        if (followUpNotifs.Count > 0) await _notifSender.SendManyAsync(followUpNotifs);

        // 1d. Mark missed follow-ups + auto-create recurrences
        var missedFollowUps = await _db.FollowUps
            .Where(f => f.Status == FollowUpStatus.Pending && f.ScheduledAt < now)
            .ToListAsync();

        foreach (var f in missedFollowUps)
        {
            f.Status = FollowUpStatus.Missed;

            BackgroundJob.Enqueue<IWebhookDispatcher>(d =>
                d.FireAsync(f.TenantId, WebhookEvent.FollowUpDue,
                    new { followUpId = f.Id, f.LeadId, f.AssignedToId, scheduledAt = f.ScheduledAt }));

            if (f.IsRecurring && !string.IsNullOrWhiteSpace(f.RecurrenceRule))
            {
                var nextDate = f.RecurrenceRule.ToUpperInvariant() switch
                {
                    "DAILY"   => f.ScheduledAt.AddDays(1),
                    "WEEKLY"  => f.ScheduledAt.AddDays(7),
                    "MONTHLY" => f.ScheduledAt.AddMonths(1),
                    _         => (DateTime?)null
                };
                if (nextDate.HasValue && nextDate.Value > now)
                    _db.FollowUps.Add(new FollowUp
                    {
                        TenantId = f.TenantId, LeadId = f.LeadId, AssignedToId = f.AssignedToId,
                        ScheduledAt = nextDate.Value, Channel = f.Channel, Notes = f.Notes,
                        IsRecurring = true, RecurrenceRule = f.RecurrenceRule
                    });
            }
        }

        // 1e. Notify newly overdue tasks (real-time push)
        var overdueNotifs = overdue.Take(50)
            .Select(t => new Notification
            {
                TenantId = t.TenantId, UserId = t.AssignedToId,
                Type = NotificationType.TaskOverdue,
                Title = "Task Overdue",
                Body = $"\"{t.Title}\" was due on {t.DueAt:dd MMM HH:mm}.",
                Link = "/Tasks"
            }).ToList();
        if (overdueNotifs.Count > 0) await _notifSender.SendManyAsync(overdueNotifs);

        // 1f. Auto-escalate based on active rules
        var activeRules = await _db.EscalationRules.Where(r => r.IsActive).ToListAsync();

        foreach (var rule in activeRules)
        {
            switch (rule.Trigger)
            {
                case EscalationTrigger.MissedFollowUp:
                {
                    var missedCounts = await _db.FollowUps
                        .Where(f => f.TenantId == rule.TenantId && f.Status == FollowUpStatus.Missed)
                        .GroupBy(f => f.LeadId)
                        .Where(g => g.Count() >= rule.ThresholdValue)
                        .Select(g => g.Key)
                        .ToListAsync();

                    foreach (var leadId in missedCounts)
                    {
                        var alreadyOpen = await _db.Escalations.AnyAsync(e =>
                            e.TenantId == rule.TenantId && e.LeadId == leadId
                            && e.RuleId == rule.Id && e.Status == EscalationStatus.Pending);
                        if (alreadyOpen) continue;

                        var lead = await _db.Leads.FindAsync(leadId);
                        if (lead == null) continue;

                        _db.Escalations.Add(new Escalation
                        {
                            TenantId = rule.TenantId, LeadId = leadId,
                            AssignedAgentId = lead.AssignedToId ?? rule.EscalateToId,
                            EscalatedToId = rule.EscalateToId, RuleId = rule.Id,
                            Reason = $"Lead has {rule.ThresholdValue}+ missed follow-ups."
                        });
                        await _db.SaveChangesAsync();
                        await _notifSender.SendAsync(new Notification
                        {
                            TenantId = rule.TenantId, UserId = rule.EscalateToId,
                            Type = NotificationType.SystemAlert,
                            Title = "Auto Escalation: Missed Follow-ups",
                            Body = $"Lead \"{lead.Name}\" has {rule.ThresholdValue}+ missed follow-ups.",
                            Link = $"/Leads/Timeline/{leadId}"
                        });
                    }
                    break;
                }

                case EscalationTrigger.NoContactDays:
                {
                    var cutoff = now.AddDays(-rule.ThresholdValue);
                    var neglectedLeads = await _db.Leads
                        .Where(l => l.TenantId == rule.TenantId
                                 && l.Status != LeadStatus.Converted
                                 && l.Status != LeadStatus.Dead
                                 && (l.LastContactedAt == null || l.LastContactedAt < cutoff))
                        .Select(l => new { l.Id, l.Name, l.AssignedToId })
                        .ToListAsync();

                    foreach (var lead in neglectedLeads)
                    {
                        var alreadyOpen = await _db.Escalations.AnyAsync(e =>
                            e.TenantId == rule.TenantId && e.LeadId == lead.Id
                            && e.RuleId == rule.Id && e.Status == EscalationStatus.Pending);
                        if (alreadyOpen) continue;

                        _db.Escalations.Add(new Escalation
                        {
                            TenantId = rule.TenantId, LeadId = lead.Id,
                            AssignedAgentId = lead.AssignedToId ?? rule.EscalateToId,
                            EscalatedToId = rule.EscalateToId, RuleId = rule.Id,
                            Reason = $"No contact for {rule.ThresholdValue}+ days."
                        });
                        await _db.SaveChangesAsync();
                        await _notifSender.SendAsync(new Notification
                        {
                            TenantId = rule.TenantId, UserId = rule.EscalateToId,
                            Type = NotificationType.SystemAlert,
                            Title = "Auto Escalation: No Contact",
                            Body = $"Lead \"{lead.Name}\" not contacted for {rule.ThresholdValue}+ days.",
                            Link = $"/Leads/Timeline/{lead.Id}"
                        });
                    }
                    break;
                }
            }
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation(
            "ProcessDueItems: {Overdue} overdue tasks, {Missed} missed follow-ups, {FollowUps} follow-up notifs.",
            overdue.Count, missedFollowUps.Count, followUpsDue.Count);
    }

    // ?????????????????????????????????????????????????????????????????????????
    // 2. Meeting reminders (every 5 minutes)
    // ?????????????????????????????????????????????????????????????????????????
    [AutomaticRetry(Attempts = 2)]
    public async Task ProcessMeetingRemindersAsync()
    {
        var now = DateTime.UtcNow;

        var upcomingMeetings = await _db.Meetings
            .Include(m => m.Attendees)
            .Include(m => m.Lead)
            .Where(m => m.Status == MeetingStatus.Scheduled
                     && m.ScheduledAt >= now
                     && m.ScheduledAt <= now.AddMinutes(30))
            .ToListAsync();

        var notifCount = 0;
        var meetingNotifs = new List<Notification>();

        foreach (var meeting in upcomingMeetings)
        {
            var minutesAway = (int)(meeting.ScheduledAt - now).TotalMinutes;
            var userIds = meeting.Attendees.Select(a => a.UserId).ToHashSet();
            userIds.Add(meeting.OrganisedById);

            foreach (var userId in userIds)
            {
                var already = await _db.Notifications.AnyAsync(n =>
                    n.UserId == userId &&
                    n.Type == NotificationType.MeetingDue &&
                    n.Link != null && n.Link.Contains(meeting.Id.ToString()) &&
                    n.CreatedAt >= now.AddMinutes(-31));
                if (already) continue;

                var leadName = meeting.Lead?.Name ?? "a lead";
                meetingNotifs.Add(new Notification
                {
                    TenantId = meeting.TenantId,
                    UserId   = userId,
                    Type     = NotificationType.MeetingDue,
                    Title    = "Meeting Starting Soon",
                    Body     = $"\"{meeting.Title}\" with {leadName} starts in {minutesAway} minutes." +
                               (string.IsNullOrEmpty(meeting.MeetingLink)
                                   ? string.Empty
                                   : $" Link: {meeting.MeetingLink}"),
                    Link     = $"/Leads/Timeline/{meeting.LeadId}"
                });
                notifCount++;
            }
        }

        if (meetingNotifs.Count > 0) await _notifSender.SendManyAsync(meetingNotifs);

        _logger.LogInformation(
            "ProcessMeetingReminders: {Count} reminder(s) for {Meetings} meeting(s).",
            notifCount, upcomingMeetings.Count);
    }

    // ?????????????????????????????????????????????????????????????????????????
    // 3. Auto-transcribe recorded calls (every 10 minutes)
    // ?????????????????????????????????????????????????????????????????????????
    [AutomaticRetry(Attempts = 2)]
    public async Task AutoTranscribeCallsAsync()
    {
        var untranscribed = await _db.Calls
            .Where(c => c.IsRecorded
                     && !string.IsNullOrEmpty(c.AudioFileUrl)
                     && string.IsNullOrEmpty(c.TranscriptText))
            .Select(c => new { c.Id, c.TenantId })
            .Take(20)
            .ToListAsync();

        foreach (var c in untranscribed)
            BackgroundJob.Enqueue<ICallAiProcessor>(p => p.ProcessAsync(c.Id, c.TenantId));

        if (untranscribed.Count > 0)
            _logger.LogInformation("AutoTranscribeCalls: queued {Count} call(s) for transcription.", untranscribed.Count);
    }

    // ?????????????????????????????????????????????????????????????????????????
    // 4. Re-score leads after recent call activity (every 2 hours)
    // ?????????????????????????????????????????????????????????????????????????
    [AutomaticRetry(Attempts = 1)]
    public async Task RescoreLeadsAsync()
    {
        var cutoff = DateTime.UtcNow.AddHours(-2);
        var tenants = await _db.Tenants
            .Where(t => t.IsActive && t.OpenRouterApiKey != null)
            .ToListAsync();

        var scored = 0;
        foreach (var tenant in tenants)
        {
            var leadIds = await _db.Calls
                .Where(c => c.TenantId == tenant.Id && c.StartedAt >= cutoff)
                .Select(c => c.LeadId)
                .Distinct()
                .ToListAsync();

            foreach (var leadId in leadIds)
            {
                var lead = await _db.Leads
                    .Include(l => l.Calls.OrderByDescending(c => c.StartedAt).Take(3))
                    .FirstOrDefaultAsync(l => l.Id == leadId);
                if (lead == null) continue;

                try
                {
                    var summaries = lead.Calls
                        .Select(c => $"Outcome:{c.Outcome} Sentiment:{c.AiSentiment}")
                        .ToList();
                    var prompt =
                        $"Rate this lead's conversion probability 0-100. " +
                        $"Lead: {lead.Name}, Status: {lead.Status}, Priority: {lead.Priority}, " +
                        $"Calls: {summaries.Count}, Recent: {string.Join("; ", summaries)}. " +
                        "Reply with ONLY a number 0-100.";
                    var scoreStr = await _ai.ChatAsync(prompt, null, tenant);
                    if (int.TryParse(scoreStr.Trim().Split(' ')[0], out var score))
                        lead.AiScore = Math.Clamp(score, 0, 100);
                    lead.UpdatedAt = DateTime.UtcNow;
                    scored++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "RescoreLeads: failed for lead {LeadId}", leadId);
                }
            }

            if (leadIds.Count > 0) await _db.SaveChangesAsync();
        }

        _logger.LogInformation("RescoreLeads: re-scored {Count} lead(s).", scored);
    }

    // ?????????????????????????????????????????????????????????????????????????
    // 5. Daily digest email (08:00 UTC every day)
    // ?????????????????????????????????????????????????????????????????????????
    [AutomaticRetry(Attempts = 2)]
    public async Task SendDailyDigestAsync()
    {
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var today     = DateTime.UtcNow.Date;

        var tenants = await _db.Tenants.Where(t => t.IsActive).ToListAsync();

        foreach (var tenant in tenants)
        {
            var callCount = await _db.Calls.CountAsync(c =>
                c.TenantId == tenant.Id && c.StartedAt >= yesterday && c.StartedAt < today);

            var conversions = await _db.Calls.CountAsync(c =>
                c.TenantId == tenant.Id && c.StartedAt >= yesterday && c.StartedAt < today
                && c.Outcome == CallOutcome.Converted);

            var openEscalations = await _db.Escalations.CountAsync(e =>
                e.TenantId == tenant.Id && e.Status == EscalationStatus.Pending);

            var dueFollowUps = await _db.FollowUps.CountAsync(f =>
                f.TenantId == tenant.Id && f.Status == FollowUpStatus.Pending
                && f.ScheduledAt >= today && f.ScheduledAt < today.AddDays(1));

            var recipients = await _db.Users
                .Where(u => u.TenantId == tenant.Id && u.IsActive
                         && (u.Role == "admin" || u.Role == "manager")
                         && u.Email != null)
                .Select(u => new { u.FullName, u.Email })
                .ToListAsync();

            if (!recipients.Any()) continue;

            var subject = $"[TelecallingCRM] Daily Digest - {yesterday:dd MMM yyyy}";
            var body =
                $"<h2 style='color:#4f46e5;'>Daily CRM Digest</h2>" +
                $"<p>Summary for <strong>{tenant.Name}</strong> on <strong>{yesterday:dd MMM yyyy}</strong>.</p>" +
                $"<table style='border-collapse:collapse;width:100%;max-width:480px;'>" +
                $"<tr style='background:#f8fafc;'><td style='padding:8px 12px;font-weight:600;'>Calls Made</td><td style='padding:8px 12px;'>{callCount}</td></tr>" +
                $"<tr><td style='padding:8px 12px;font-weight:600;'>Conversions</td><td style='padding:8px 12px;'>{conversions}</td></tr>" +
                $"<tr style='background:#f8fafc;'><td style='padding:8px 12px;font-weight:600;'>Open Escalations</td><td style='padding:8px 12px;'>{openEscalations}</td></tr>" +
                $"<tr><td style='padding:8px 12px;font-weight:600;'>Follow-ups Due Today</td><td style='padding:8px 12px;'>{dueFollowUps}</td></tr>" +
                $"</table>" +
                $"<p style='margin-top:1rem;'>Log in to <a href='https://app.telecallingcrm.app'>TelecallingCRM</a> to take action.</p>";

            foreach (var r in recipients)
            {
                try
                {
                    await _messageDispatcher.SendEmailAsync(tenant.Id, r.Email!, subject, body);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "DailyDigest: failed to email {Email}", r.Email);
                }
            }
        }

        _logger.LogInformation("SendDailyDigestAsync: completed for {Count} tenant(s).", tenants.Count);
    }

    // ?????????????????????????????????????????????????????????????????????????
    // Job registration — call once from Program.cs
    // ?????????????????????????????????????????????????????????????????????????
    public static void RegisterRecurringJobs()
    {
        RecurringJob.AddOrUpdate<ScheduledJobService>(
            "process-due-items",
            s => s.ProcessDueItemsAsync(),
            "*/5 * * * *");

        RecurringJob.AddOrUpdate<ScheduledJobService>(
            "process-meeting-reminders",
            s => s.ProcessMeetingRemindersAsync(),
            "*/5 * * * *");

        RecurringJob.AddOrUpdate<ScheduledJobService>(
            "auto-transcribe-calls",
            s => s.AutoTranscribeCallsAsync(),
            "*/10 * * * *");

        RecurringJob.AddOrUpdate<ScheduledJobService>(
            "rescore-leads",
            s => s.RescoreLeadsAsync(),
            "0 */2 * * *");

        RecurringJob.AddOrUpdate<ScheduledJobService>(
            "daily-digest",
            s => s.SendDailyDigestAsync(),
            "0 8 * * *");
    }
}
