using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Hubs;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class CallsEndpoints
{
    public static void MapCallsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/calls").WithTags("Calls").RequireAuthorization().RequireRateLimiting("api");

        group.MapGet("/", async (TenantContext tc, AppDbContext db,
            [FromQuery] Guid? leadId, [FromQuery] Guid? agentId,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var query = db.Calls.Where(c => c.TenantId == tc.TenantId).AsQueryable();
            if (leadId.HasValue) query = query.Where(c => c.LeadId == leadId);
            if (agentId.HasValue) query = query.Where(c => c.AgentId == agentId);

            var total = await query.CountAsync();
            var calls = await query
                .OrderByDescending(c => c.StartedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(c => new
                {
                    c.Id, c.StartedAt, c.EndedAt, c.DurationSeconds,
                    c.Outcome, c.Notes, c.AiSummary, c.AiSentiment,
                    c.TranscriptText, c.AudioFileUrl,
                    Lead = c.Lead.Name, LeadPhone = c.Lead.Phone,
                    Agent = c.Agent.FullName
                }).ToListAsync();

            return Results.Ok(new { total, page, pageSize, calls });
        });

        // Start a call session
        group.MapPost("/start", async ([FromBody] StartCallDto dto, TenantContext tc,
            AppDbContext db, IHubContext<CrmHub> hub) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            // ?? DNC guard ????????????????????????????????????????????????????
            var lead = await db.Leads.FindAsync(dto.LeadId);
            if (lead != null)
            {
                var normPhone = DncEndpoints.NormalisePhone(lead.Phone);
                var isDnc = await db.DncEntries
                    .AnyAsync(d => d.TenantId == tc.TenantId && d.Phone == normPhone);
                if (isDnc)
                    return Results.BadRequest(new {
                        error = "DNC",
                        message = $"Cannot call {lead.Phone} — this number is on the Do-Not-Call list."
                    });
            }

            var call = new Call
            {
                TenantId = tc.TenantId,
                LeadId = dto.LeadId,
                AgentId = dto.AgentId,
                StartedAt = DateTime.UtcNow
            };
            db.Calls.Add(call);

            // Update lead status to Contacted and record last contact time
            if (lead != null)
            {
                if (lead.Status == LeadStatus.New)
                    lead.Status = LeadStatus.Contacted;
                lead.LastContactedAt = DateTime.UtcNow;
            }

            db.ActivityLogs.Add(new ActivityLog {
                TenantId = tc.TenantId, LeadId = dto.LeadId, UserId = dto.AgentId,
                Type = ActivityType.CallMade, Summary = "Call started"
            });

            await db.SaveChangesAsync();

            // Notify all tenant members
            await hub.Clients.Group($"tenant-{tc.TenantId}")
                .SendAsync("CallStarted", new { call.Id, dto.LeadId, dto.AgentId });

            return Results.Created($"/api/calls/{call.Id}", new { call.Id });
        });

        // End a call + attach transcript + trigger AI analysis
        group.MapPost("/{id:guid}/end", async (Guid id, [FromBody] EndCallDto dto,
            TenantContext tc, AppDbContext db, IHubContext<CrmHub> hub, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var call = await db.Calls
                .Include(c => c.Lead)
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tc.TenantId);
            if (call == null) return Results.NotFound();

            call.EndedAt = DateTime.UtcNow;
            call.DurationSeconds = (int)(call.EndedAt.Value - call.StartedAt).TotalSeconds;
            call.Outcome = dto.Outcome;
            call.Notes = dto.Notes;

            // Store transcript now; AI analysis runs in background (non-blocking)
            if (!string.IsNullOrWhiteSpace(dto.TranscriptText))
                call.TranscriptText = dto.TranscriptText;

            // Update lead status and last contact time based on outcome
            if (call.Lead != null)
            {
                call.Lead.Status = dto.Outcome switch
                {
                    CallOutcome.Interested    => LeadStatus.Interested,
                    CallOutcome.Converted     => LeadStatus.Converted,
                    CallOutcome.NotInterested => LeadStatus.NotInterested,
                    CallOutcome.HotLead       => LeadStatus.Interested,
                    CallOutcome.Callback      => LeadStatus.FollowUp,
                    CallOutcome.CallLater     => LeadStatus.FollowUp,
                    _                         => call.Lead.Status
                };
                call.Lead.LastContactedAt = DateTime.UtcNow;
                call.Lead.UpdatedAt = DateTime.UtcNow;
            }

            db.ActivityLogs.Add(new ActivityLog {
                TenantId = tc.TenantId, LeadId = call.LeadId, UserId = call.AgentId,
                Type = ActivityType.CallMade,
                Summary = $"Call ended — {dto.Outcome} — {call.DurationSeconds}s"
            });

            await db.SaveChangesAsync();

            // Queue AI analysis as a background job so the agent's UI isn't blocked
            if (!string.IsNullOrWhiteSpace(dto.TranscriptText))
                Hangfire.BackgroundJob.Enqueue<ICallAiProcessor>(p => p.ProcessAsync(call.Id, tc.TenantId));

            await hub.Clients.Group($"tenant-{tc.TenantId}")
                .SendAsync("CallEnded", new { id, call.Outcome, call.DurationSeconds });

            // Push real-time dashboard update
            await hub.Clients.Group($"tenant-{tc.TenantId}")
                .SendAsync("DashboardUpdated", new { reason = "call_ended" });

            // Fire webhook for CallCompleted
            var webhookDispatcher = http.RequestServices.GetRequiredService<IWebhookDispatcher>();
            Hangfire.BackgroundJob.Enqueue(() => webhookDispatcher.FireAsync(
                tc.TenantId, WebhookEvent.CallCompleted,
                new { callId = id, call.Outcome, call.DurationSeconds, call.LeadId, call.AgentId }));

            return Results.Ok(new { call.Id, call.DurationSeconds, status = "AI analysis queued" });
        });

        // Upload audio ? Whisper STT ? return transcript
        group.MapPost("/{id:guid}/transcribe", async (Guid id, IFormFile audio,
            TenantContext tc, AppDbContext db, IWhisperService whisper) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var call = await db.Calls.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tc.TenantId);
            if (call == null) return Results.NotFound();

            var tenant = await db.Tenants.FindAsync(tc.TenantId);
            var transcript = await whisper.TranscribeAsync(audio, tenant?.OpenRouterApiKey ?? string.Empty);
            call.TranscriptText = transcript;
            await db.SaveChangesAsync();

            return Results.Ok(new { transcript });
        }).DisableAntiforgery();

        // Get full call detail (transcript, AI summary, sentiment)
        group.MapGet("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var call = await db.Calls
                .Include(c => c.Lead)
                .Include(c => c.Agent)
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tc.TenantId);
            if (call == null) return Results.NotFound();
            return Results.Ok(new {
                call.Id, call.StartedAt, call.EndedAt, call.DurationSeconds,
                call.Direction, call.Outcome, call.Notes,
                call.TranscriptText, call.AudioFileUrl,
                call.AiSummary, call.AiSentiment, call.AiInsight,
                call.IsRecorded,
                Lead = new { call.Lead.Id, call.Lead.Name, call.Lead.Phone },
                Agent = call.Agent.FullName
            });
        });

        // Update call notes after save
        group.MapPatch("/{id:guid}/notes", async (Guid id, [FromBody] PatchNotesDto dto,
            TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var call = await db.Calls.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tc.TenantId);
            if (call == null) return Results.NotFound();
            call.Notes = dto.Notes;
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            db.ActivityLogs.Add(new ActivityLog {
                TenantId = tc.TenantId, LeadId = call.LeadId, UserId = userId,
                Type = ActivityType.NoteAdded, Summary = "Call notes updated"
            });
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // Get next lead to call in a campaign (predictive dialer queue)
        group.MapGet("/next-lead", async (TenantContext tc, AppDbContext db, HttpContext http,
            [FromQuery] Guid? campaignId) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var query = db.Leads
                .Where(l => l.TenantId == tc.TenantId
                         && l.AssignedToId == userId
                         && l.Status != LeadStatus.Converted
                         && l.Status != LeadStatus.Dead
                         && l.Status != LeadStatus.NotInterested)
                .AsQueryable();

            if (campaignId.HasValue)
                query = query.Where(l => l.CampaignId == campaignId);

            var lead = await query
                .OrderByDescending(l => l.Priority)
                .ThenByDescending(l => l.AiScore)
                .ThenBy(l => l.LastContactedAt == null ? DateTime.MinValue : l.LastContactedAt)
                .Select(l => new { l.Id, l.Name, l.Phone, l.AlternatePhone, l.Status, l.Priority, l.AiScore, l.AiInsight, l.NextFollowUpAt })
                .FirstOrDefaultAsync();

            return lead == null ? Results.NotFound(new { message = "No leads in queue" }) : Results.Ok(lead);
        });

        // Inbound call webhook (e.g. from Twilio/Exotel)
        group.MapPost("/inbound", async ([FromBody] InboundCallDto dto, AppDbContext db) =>
        {
            var lead = await db.Leads.FirstOrDefaultAsync(l => l.Phone == dto.FromPhone);
            if (lead == null) return Results.Ok(new { message = "Lead not found" });

            var call = new Call
            {
                TenantId = lead.TenantId,
                LeadId = lead.Id,
                AgentId = lead.AssignedToId ?? Guid.Empty,
                Direction = CallDirection.Inbound,
                StartedAt = DateTime.UtcNow,
                ProviderCallId = dto.CallId
            };
            db.Calls.Add(call);
            lead.LastContactedAt = DateTime.UtcNow;
            db.ActivityLogs.Add(new ActivityLog {
                TenantId = lead.TenantId, LeadId = lead.Id, UserId = call.AgentId,
                Type = ActivityType.CallMade, Summary = $"Inbound call from {dto.FromPhone}"
            });
            await db.SaveChangesAsync();
            return Results.Ok(new { call.Id });
        }).AllowAnonymous();
    }
}

public record StartCallDto(Guid LeadId, Guid AgentId);
public record EndCallDto(CallOutcome Outcome, string? Notes, string? TranscriptText);
public record PatchNotesDto(string? Notes);
public record InboundCallDto(string FromPhone, string? CallId);

