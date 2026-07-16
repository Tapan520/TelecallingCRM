using Hangfire;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using EscalationStatus = TelecallingCRM.Data.Models.EscalationStatus;

namespace TelecallingCRM.Services;

/// <summary>
/// Hangfire recurring jobs for:
/// - Auto-flagging overdue tasks
/// - Creating FollowUpDue notifications 30 min before scheduled follow-ups
/// - Creating TaskDue notifications 1 hour before task due time
/// </summary>
public class ScheduledJobService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ScheduledJobService> _logger;
    private readonly IWebhookDispatcher _webhookDispatcher;

    public ScheduledJobService(AppDbContext db, ILogger<ScheduledJobService> logger, IWebhookDispatcher webhookDispatcher)
    {
        _db = db;
        _logger = logger;
        _webhookDispatcher = webhookDispatcher;
    }

    /// <summary>Runs every 5 minutes. Flags overdue tasks and sends due notifications.</summary>
    [AutomaticRetry(Attempts = 2)]
    public async Task ProcessDueItemsAsync()
    {
        var now = DateTime.UtcNow;
        var notifWindow = now.AddMinutes(30);

        // ?? 1. Mark overdue tasks ??????????????????????????????????????????????
        var overdue = await _db.Tasks
            .Where(t => t.Status == TelecallingCRM.Data.Models.TaskStatus.Pending && t.DueAt < now)
            .ToListAsync();
        foreach (var t in overdue)
            t.Status = TelecallingCRM.Data.Models.TaskStatus.Overdue;

        // ?? 2. Notify for tasks due within 30 minutes ?????????????????????????
        var tasksDue = await _db.Tasks
            .Where(t => t.Status == TelecallingCRM.Data.Models.TaskStatus.Pending
                     && t.DueAt >= now && t.DueAt <= notifWindow)
            .ToListAsync();

        foreach (var t in tasksDue)
        {
            var alreadyNotified = await _db.Notifications.AnyAsync(n =>
                n.UserId == t.AssignedToId &&
                n.Type == NotificationType.TaskDue &&
                n.Link != null && n.Link.Contains(t.Id.ToString()) &&
                n.CreatedAt >= now.AddMinutes(-31));

            if (!alreadyNotified)
                _db.Notifications.Add(new Notification {
                    TenantId = t.TenantId, UserId = t.AssignedToId,
                    Type = NotificationType.TaskDue,
                    Title = "Task Due Soon",
                    Body = $"\"{t.Title}\" is due in {(int)(t.DueAt - now).TotalMinutes} minutes.",
                    Link = $"/Tasks"
                });
        }

        // ?? 3. Notify for follow-ups due within 30 minutes ????????????????????
        var followUpsDue = await _db.FollowUps
            .Where(f => f.Status == FollowUpStatus.Pending
                     && f.ScheduledAt >= now && f.ScheduledAt <= notifWindow)
            .Include(f => f.Lead)
            .ToListAsync();

        foreach (var f in followUpsDue)
        {
            var alreadyNotified = await _db.Notifications.AnyAsync(n =>
                n.UserId == f.AssignedToId &&
                n.Type == NotificationType.FollowUpDue &&
                n.Link != null && n.Link.Contains(f.LeadId.ToString()) &&
                n.CreatedAt >= now.AddMinutes(-31));

            if (!alreadyNotified)
                _db.Notifications.Add(new Notification {
                    TenantId = f.TenantId, UserId = f.AssignedToId,
                    Type = NotificationType.FollowUpDue,
                    Title = "Follow-up Due Soon",
                    Body = $"Follow-up with {f.Lead.Name} via {f.Channel} in {(int)(f.ScheduledAt - now).TotalMinutes} minutes.",
                    Link = $"/Leads/Timeline/{f.LeadId}"
                });
        }

        // ?? 4. Mark missed follow-ups and auto-create next recurrence ???????????
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
                {
                    _db.FollowUps.Add(new FollowUp {
                        TenantId = f.TenantId, LeadId = f.LeadId, AssignedToId = f.AssignedToId,
                        ScheduledAt = nextDate.Value, Channel = f.Channel, Notes = f.Notes,
                        IsRecurring = true, RecurrenceRule = f.RecurrenceRule
                    });
                }
            }
        }

        // ?? 5. Notify overdue tasks ????????????????????????????????????????????
        var newlyOverdue = overdue.Take(50).ToList();
        foreach (var t in newlyOverdue)
            _db.Notifications.Add(new Notification {
                TenantId = t.TenantId, UserId = t.AssignedToId,
                Type = NotificationType.TaskOverdue,
                Title = "Task Overdue",
                Body = $"\"{t.Title}\" was due on {t.DueAt:dd MMM HH:mm}.",
                Link = $"/Tasks"
            });

        // ?? 6. Auto-escalate leads based on active escalation rules ????????
        var activeRules = await _db.EscalationRules
            .Where(r => r.IsActive)
            .ToListAsync();

        foreach (var rule in activeRules)
        {
            switch (rule.Trigger)
            {
                case EscalationTrigger.MissedFollowUp:
                {
                    // Leads with >= ThresholdValue missed follow-ups that are not yet escalated
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
                            && e.RuleId == rule.Id
                            && e.Status == EscalationStatus.Pending);
                        if (alreadyOpen) continue;

                        var lead = await _db.Leads.FindAsync(leadId);
                        if (lead == null) continue;

                        _db.Escalations.Add(new Escalation {
                            TenantId = rule.TenantId, LeadId = leadId,
                            AssignedAgentId = lead.AssignedToId ?? rule.EscalateToId,
                            EscalatedToId = rule.EscalateToId, RuleId = rule.Id,
                            Reason = $"Lead has {rule.ThresholdValue}+ missed follow-ups."
                        });
                        _db.Notifications.Add(new Notification {
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
                            && e.RuleId == rule.Id
                            && e.Status == EscalationStatus.Pending);
                        if (alreadyOpen) continue;

                        _db.Escalations.Add(new Escalation {
                            TenantId = rule.TenantId, LeadId = lead.Id,
                            AssignedAgentId = lead.AssignedToId ?? rule.EscalateToId,
                            EscalatedToId = rule.EscalateToId, RuleId = rule.Id,
                            Reason = $"No contact for {rule.ThresholdValue}+ days."
                        });
                        _db.Notifications.Add(new Notification {
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
        _logger.LogInformation("ScheduledJobService: {Overdue} overdue tasks, {Missed} missed follow-ups, {FollowUps} follow-up notifications",
            overdue.Count, missedFollowUps.Count, followUpsDue.Count);
    }

    /// <summary>Registers all recurring jobs. Call from Program.cs after Hangfire is configured.</summary>
    public static void RegisterRecurringJobs()
    {
        RecurringJob.AddOrUpdate<ScheduledJobService>(
            "process-due-items",
            s => s.ProcessDueItemsAsync(),
            "*/5 * * * *"); // every 5 minutes

        RecurringJob.AddOrUpdate<ScheduledJobService>(
            "process-meeting-reminders",
            s => s.ProcessMeetingRemindersAsync(),
            "*/5 * * * *"); // every 5 minutes
    }

    /// <summary>
    /// Runs every 5 minutes. Sends reminder notifications to the organiser and
    /// all attendees 30 minutes before a scheduled meeting starts.
    /// </summary>
    [AutomaticRetry(Attempts = 2)]
    public async Task ProcessMeetingRemindersAsync()
    {
        var now = DateTime.UtcNow;
        var windowStart = now;
        var windowEnd   = now.AddMinutes(30);

        // Meetings that start within the next 30 minutes and are still Scheduled
        var upcomingMeetings = await _db.Meetings
            .Include(m => m.Attendees)
            .Include(m => m.Lead)
            .Where(m => m.Status == MeetingStatus.Scheduled
                     && m.ScheduledAt >= windowStart
                     && m.ScheduledAt <= windowEnd)
            .ToListAsync();

        var notifCount = 0;

        foreach (var meeting in upcomingMeetings)
        {
            var minutesAway = (int)(meeting.ScheduledAt - now).TotalMinutes;
            // Collect every user that should be notified: organiser + all attendees
            var userIds = meeting.Attendees.Select(a => a.UserId).ToHashSet();
            userIds.Add(meeting.OrganisedById);

            foreach (var userId in userIds)
            {
                // De-duplicate: don't send a second reminder if one was sent in the last 31 min
                var alreadyNotified = await _db.Notifications.AnyAsync(n =>
                    n.UserId == userId &&
                    n.Type == NotificationType.MeetingDue &&
                    n.Link != null && n.Link.Contains(meeting.Id.ToString()) &&
                    n.CreatedAt >= now.AddMinutes(-31));

                if (alreadyNotified) continue;

                var leadName = meeting.Lead?.Name ?? "a lead";
                _db.Notifications.Add(new Notification
                {
                    TenantId   = meeting.TenantId,
                    UserId     = userId,
                    Type       = NotificationType.MeetingDue,
                    Title      = "Meeting Starting Soon",
                    Body       = $"\"{meeting.Title}\" with {leadName} starts in {minutesAway} minutes." +
                                 (string.IsNullOrEmpty(meeting.MeetingLink)
                                     ? string.Empty
                                     : $" Link: {meeting.MeetingLink}"),
                    Link       = $"/Leads/Timeline/{meeting.LeadId}"
                });
                notifCount++;
            }
        }

        if (notifCount > 0)
            await _db.SaveChangesAsync();

        _logger.LogInformation(
            "ScheduledJobService.ProcessMeetingRemindersAsync: {Count} reminder(s) sent for {Meetings} upcoming meeting(s).",
            notifCount, upcomingMeetings.Count);
    }
}
