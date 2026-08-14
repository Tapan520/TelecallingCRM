using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Hubs;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class DialerEndpoints
{
    public static void MapDialerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dialer").WithTags("Dialer")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        // ?? GET /api/dialer/status ???????????????????????????????????????????
        // Returns whether Exotel is configured so the UI can show the right message.
        group.MapGet("/status", async (TenantContext tc, IExotelVoiceService exotel) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var configured = await exotel.IsConfiguredAsync(tc.TenantId);
            return Results.Ok(new { provider = "exotel", configured });
        });

        // ?? POST /api/dialer/call ????????????????????????????????????????????
        // Initiates an Exotel Click-to-Call:
        //   1. Exotel calls the agent's phone first
        //   2. Agent picks up ? Exotel bridges to lead's phone
        //   3. Call record is created in DB
        group.MapPost("/call", async ([FromBody] DialerCallDto dto, TenantContext tc,
            HttpContext http, AppDbContext db,
            IExotelVoiceService exotel, IHubContext<CrmHub> hub) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var agentUserId = Guid.Parse(
                http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            // Load lead — validates it belongs to this tenant
            var lead = await db.Leads
                .FirstOrDefaultAsync(l => l.Id == dto.LeadId && l.TenantId == tc.TenantId);
            if (lead == null)
                return Results.NotFound(new { error = "Lead not found." });

            // DNC check
            var normPhone = new string(lead.Phone.Where(char.IsDigit).ToArray());
            var isDnc = await db.DncEntries
                .AnyAsync(d => d.TenantId == tc.TenantId && d.Phone == normPhone);
            if (isDnc)
                return Results.BadRequest(new
                {
                    error = "DNC",
                    message = $"Cannot call {lead.Phone} – this number is on the Do-Not-Call list."
                });

            // Agent's phone number (stored in IdentityUser.PhoneNumber)
            var agent = await db.Users.FindAsync(agentUserId);
            var agentPhone = dto.AgentPhone ?? agent?.PhoneNumber;
            if (string.IsNullOrWhiteSpace(agentPhone))
                return Results.BadRequest(new
                {
                    error = "AgentPhoneMissing",
                    message = "Agent phone number is required for Click-to-Call. " +
                              "Please update it in your Profile."
                });

            // Initiate Exotel Click-to-Call
            var (success, callSid, callError) =
                await exotel.ClickToCallAsync(tc.TenantId, agentPhone, lead.Phone);

            if (!success)
                return Results.BadRequest(new { error = callError });

            // Save Call record to DB
            var call = new Call
            {
                TenantId        = tc.TenantId,
                LeadId          = dto.LeadId,
                AgentId         = agentUserId,
                Direction       = CallDirection.Outbound,
                StartedAt       = DateTime.UtcNow,
                ProviderCallId  = callSid
            };
            db.Calls.Add(call);

            // Update lead status
            if (lead.Status == LeadStatus.New)
                lead.Status = LeadStatus.Contacted;
            lead.LastContactedAt = DateTime.UtcNow;

            db.ActivityLogs.Add(new ActivityLog
            {
                TenantId = tc.TenantId,
                LeadId   = dto.LeadId,
                UserId   = agentUserId,
                Type     = ActivityType.CallMade,
                Summary  = $"Click-to-Call initiated via Exotel ? {lead.Phone}"
            });

            await db.SaveChangesAsync();

            await hub.Clients.Group($"tenant-{tc.TenantId}")
                .SendAsync("CallStarted", new { call.Id, dto.LeadId, agentUserId });

            return Results.Ok(new
            {
                callId    = call.Id,
                exotelSid = callSid,
                message   = $"Exotel is calling your phone ({agentPhone}). Pick up to connect to {lead.Name}."
            });
        });

        // ?? POST /api/dialer/manual-call ????????????????????????????????????
        // Used when Exotel is NOT configured.
        // Agent dials the lead manually from their own phone.
        // This endpoint just creates the Call record + starts the timer so
        // the disposition panel works exactly the same as the Exotel flow.
        group.MapPost("/manual-call", async ([FromBody] DialerCallDto dto, TenantContext tc,
            HttpContext http, AppDbContext db, IHubContext<CrmHub> hub) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var agentUserId = Guid.Parse(
                http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var lead = await db.Leads
                .FirstOrDefaultAsync(l => l.Id == dto.LeadId && l.TenantId == tc.TenantId);
            if (lead == null)
                return Results.NotFound(new { error = "Lead not found." });

            // DNC check
            var normPhone = new string(lead.Phone.Where(char.IsDigit).ToArray());
            var isDnc = await db.DncEntries
                .AnyAsync(d => d.TenantId == tc.TenantId && d.Phone == normPhone);
            if (isDnc)
                return Results.BadRequest(new
                {
                    error = "DNC",
                    message = $"Cannot call {lead.Phone} — this number is on the Do-Not-Call list."
                });

            var call = new Call
            {
                TenantId   = tc.TenantId,
                LeadId     = dto.LeadId,
                AgentId    = agentUserId,
                Direction  = CallDirection.Outbound,
                StartedAt  = DateTime.UtcNow
            };
            db.Calls.Add(call);

            if (lead.Status == LeadStatus.New)
                lead.Status = LeadStatus.Contacted;
            lead.LastContactedAt = DateTime.UtcNow;

            db.ActivityLogs.Add(new ActivityLog
            {
                TenantId = tc.TenantId,
                LeadId   = dto.LeadId,
                UserId   = agentUserId,
                Type     = ActivityType.CallMade,
                Summary  = $"Manual call to {lead.Phone}"
            });

            await db.SaveChangesAsync();

            await hub.Clients.Group($"tenant-{tc.TenantId}")
                .SendAsync("CallStarted", new { call.Id, dto.LeadId, agentUserId });

            return Results.Ok(new
            {
                callId  = call.Id,
                phone   = lead.Phone,
                message = $"Dial {lead.Phone} from your phone. Save disposition when done."
            });
        });

        // ?? POST /api/dialer/callback ????????????????????????????????????????
        // Exotel posts call status updates here (PassThru URL).
        // Must be AllowAnonymous — Exotel doesn't send auth headers.
        app.MapPost("/api/dialer/callback", async (HttpContext http, AppDbContext db) =>
        {
            var form = http.Request.HasFormContentType
                ? await http.Request.ReadFormAsync()
                : null;

            string? exotelSid    = form?["CallSid"]              ?? http.Request.Query["CallSid"];
            string? status       = form?["Status"]               ?? http.Request.Query["Status"];
            string? durationStr  = form?["ConversationDuration"] ?? http.Request.Query["ConversationDuration"];
            string? recordingUrl = form?["RecordingUrl"]         ?? http.Request.Query["RecordingUrl"];

            if (string.IsNullOrWhiteSpace(exotelSid))
                return Results.BadRequest("CallSid missing");

            var call = await db.Calls.FirstOrDefaultAsync(c => c.ProviderCallId == exotelSid);
            if (call != null)
            {
                call.Outcome = status?.ToLowerInvariant() switch
                {
                    "completed" => CallOutcome.Other,   // agent will set final outcome via disposition
                    "no-answer" => CallOutcome.NoAnswer,
                    "busy"      => CallOutcome.Busy,
                    "failed"    => CallOutcome.NoAnswer,
                    _           => call.Outcome
                };

                if (int.TryParse(durationStr, out var dur))
                {
                    call.DurationSeconds = dur;
                    call.EndedAt         = call.StartedAt.AddSeconds(dur);
                }

                if (!string.IsNullOrWhiteSpace(recordingUrl))
                {
                    call.AudioFileUrl = recordingUrl;
                    call.IsRecorded   = true;
                }

                await db.SaveChangesAsync();
            }

            return Results.Ok();
        }).AllowAnonymous().DisableAntiforgery();
    }
}

public record DialerCallDto(
    Guid LeadId,
    /// <summary>Override agent phone. Falls back to agent's profile PhoneNumber.</summary>
    string? AgentPhone = null
);
