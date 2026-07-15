using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class TimelineEndpoints
{
    public static void MapTimelineEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/timeline").WithTags("Timeline").RequireAuthorization().RequireRateLimiting("api");

        group.MapGet("/{leadId:guid}", async (Guid leadId, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            // Verify lead belongs to tenant
            var lead = await db.Leads.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == leadId && l.TenantId == tc.TenantId);
            if (lead == null) return Results.NotFound();

            var activities = await db.ActivityLogs
                .Where(a => a.LeadId == leadId)
                .Include(a => a.User)
                .OrderByDescending(a => a.OccurredAt)
                .Select(a => new {
                    a.Id, a.Type, a.Summary, a.Detail, a.OccurredAt,
                    By = a.User.FullName
                })
                .ToListAsync();

            var calls = await db.Calls
                .Where(c => c.LeadId == leadId)
                .Include(c => c.Agent)
                .OrderByDescending(c => c.StartedAt)
                .Select(c => new {
                    c.Id, c.StartedAt, c.DurationSeconds, c.Outcome,
                    c.Notes, c.AiSentiment, c.AiSummary, c.AiInsight,
                    Agent = c.Agent.FullName,
                    Type = "call"
                })
                .ToListAsync();

            var followups = await db.FollowUps
                .Where(f => f.LeadId == leadId)
                .Include(f => f.AssignedTo)
                .OrderByDescending(f => f.ScheduledAt)
                .Select(f => new {
                    f.Id, f.ScheduledAt, f.Channel, f.Status, f.Notes, f.CompletedAt,
                    AssignedTo = f.AssignedTo.FullName,
                    Type = "followup"
                })
                .ToListAsync();

            var tasks = await db.Tasks
                .Where(t => t.LeadId == leadId)
                .Include(t => t.AssignedTo)
                .OrderByDescending(t => t.DueAt)
                .Select(t => new {
                    t.Id, t.Title, t.Priority, t.Status, t.DueAt, t.CompletedAt,
                    AssignedTo = t.AssignedTo.FullName,
                    Type = "task"
                })
                .ToListAsync();

            var documents = await db.LeadDocuments
                .Where(d => d.LeadId == leadId)
                .Include(d => d.UploadedBy)
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new {
                    d.Id, d.FileName, d.FileUrl, d.Type, d.FileSizeBytes, d.UploadedAt,
                    UploadedBy = d.UploadedBy.FullName,
                    DocType = "document"
                })
                .ToListAsync();

            var meetings = await db.Meetings
                .Where(m => m.LeadId == leadId)
                .Include(m => m.OrganisedBy)
                .OrderByDescending(m => m.ScheduledAt)
                .Select(m => new {
                    m.Id, m.Title, m.Type, m.Status, m.ScheduledAt,
                    m.DurationMinutes, m.Location, m.MeetingLink,
                    m.Outcome, m.Notes,
                    OrganisedBy = m.OrganisedBy.FullName,
                    ItemType = "meeting"
                })
                .ToListAsync();

            var escalations = await db.Escalations
                .Where(e => e.LeadId == leadId)
                .Include(e => e.EscalatedTo)
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new {
                    e.Id, e.Status, e.Reason, e.CreatedAt,
                    e.AcknowledgedAt, e.ResolvedAt, e.ResolutionNote,
                    EscalatedTo = e.EscalatedTo.FullName,
                    ItemType = "escalation"
                })
                .ToListAsync();

            var payments = await db.Payments
                .Where(p => p.LeadId == leadId)
                .Include(p => p.RecordedBy)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new {
                    p.Id, p.Amount, p.Currency, p.Status,
                    p.Description, p.ReceiptNumber, p.CreatedAt, p.CapturedAt,
                    RecordedBy = p.RecordedBy.FullName,
                    ItemType = "payment"
                })
                .ToListAsync();

            return Results.Ok(new {
                lead = new { lead.Id, lead.Name, lead.Phone, lead.Email, lead.Status, lead.AiScore, lead.AiInsight, lead.CreatedAt },
                activities, calls, followups, tasks, documents, meetings, escalations, payments
            });
        });
    }
}
