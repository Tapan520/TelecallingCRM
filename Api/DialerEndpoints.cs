using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class DialerEndpoints
{
    public static void MapDialerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dialer").WithTags("Dialer")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        // ?? GET /api/dialer/token ????????????????????????????????????????????
        // Returns a Twilio Access Token so the browser JS SDK can place calls.
        group.MapGet("/token", async (TenantContext tc, HttpContext http,
            ITwilioVoiceService voice) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var agentId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            var (token, error) = await voice.GenerateAccessTokenAsync(tc.TenantId, $"agent-{agentId}");
            if (token == null)
                return Results.BadRequest(new { error });
            return Results.Ok(new { token });
        });

        // ?? POST /api/dialer/twiml ???????????????????????????????????????????
        // Twilio calls this webhook to get call instructions (TwiML).
        // Must be AllowAnonymous because Twilio posts here without a session cookie.
        app.MapPost("/api/dialer/twiml", ([FromForm] string? To, [FromForm] string? Called) =>
        {
            // 'To' is set when dialing via the JS SDK; 'Called' for REST-initiated calls
            var number = To ?? Called ?? string.Empty;
            var twiml = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <Response>
                    <Dial timeout="30" record="record-from-ringing" recordingStatusCallback="/api/dialer/recording-status">
                        <Number>{System.Net.WebUtility.HtmlEncode(number)}</Number>
                    </Dial>
                </Response>
                """;
            return Results.Content(twiml, "application/xml");
        }).AllowAnonymous().DisableAntiforgery();

        // ?? POST /api/dialer/recording-status ????????????????????????????????
        // Twilio posts here when a recording is ready. We save the URL to the Call record.
        app.MapPost("/api/dialer/recording-status", async (
            [FromForm] string? RecordingSid,
            [FromForm] string? RecordingUrl,
            [FromForm] string? CallSid,
            AppDbContext db) =>
        {
            if (!string.IsNullOrWhiteSpace(CallSid) && !string.IsNullOrWhiteSpace(RecordingUrl))
            {
                var call = await db.Calls.FirstOrDefaultAsync(c => c.ProviderCallId == CallSid);
                if (call != null)
                {
                    // Twilio recording URLs need .mp3 appended
                    call.AudioFileUrl = RecordingUrl + ".mp3";
                    call.IsRecorded = true;
                    await db.SaveChangesAsync();
                }
            }
            return Results.Ok();
        }).AllowAnonymous().DisableAntiforgery();
    }
}
