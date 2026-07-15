using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class WhatsAppEndpoints
{
    public static void MapWhatsAppEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/whatsapp").WithTags("WhatsApp").RequireAuthorization().RequireRateLimiting("api");

        // Send a WhatsApp message (stubs provider call — integrate Twilio/WABA)
        group.MapPost("/send", async ([FromBody] SendWhatsAppDto dto, TenantContext tc,
            AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var msg = new WhatsAppMessage
            {
                TenantId = tc.TenantId,
                LeadId = dto.LeadId,
                SentById = userId,
                ToPhone = dto.ToPhone,
                Body = dto.Body,
                TemplateId = dto.TemplateId,
                MediaUrl = dto.MediaUrl,
                Status = WhatsAppMessageStatus.Queued
            };
            db.WhatsAppMessages.Add(msg);

            // Activity log
            db.ActivityLogs.Add(new ActivityLog {
                TenantId = tc.TenantId, LeadId = dto.LeadId, UserId = userId,
                Type = ActivityType.WhatsAppSent,
                Summary = $"WhatsApp sent to {dto.ToPhone}: {dto.Body.Take(60)}..."
            });

            await db.SaveChangesAsync();

            // TODO: Dispatch via Twilio/WABA integration config
            // var config = await db.IntegrationConfigs.FirstOrDefaultAsync(i => i.TenantId == tc.TenantId && i.Provider == "twilio");
            // if (config?.IsEnabled == true) { /* call Twilio API */ }

            msg.Status = WhatsAppMessageStatus.Sent;
            await db.SaveChangesAsync();

            return Results.Ok(new { msg.Id, msg.Status });
        });

        // Get conversation history for a lead
        group.MapGet("/lead/{leadId:guid}", async (Guid leadId, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var msgs = await db.WhatsAppMessages
                .Where(w => w.LeadId == leadId && w.TenantId == tc.TenantId)
                .Include(w => w.SentBy)
                .OrderBy(w => w.SentAt)
                .Select(w => new {
                    w.Id, w.Body, w.Direction, w.Status, w.MediaUrl,
                    w.SentAt, w.DeliveredAt, w.ReadAt,
                    SentBy = w.SentBy.FullName
                })
                .ToListAsync();
            return Results.Ok(msgs);
        });

        // Inbound webhook (WhatsApp Business API posts here)
        group.MapPost("/webhook/inbound", async (HttpContext ctx, AppDbContext db) =>
        {
            // HMAC-SHA256 verification using X-Hub-Signature-256 header (Meta/WhatsApp standard)
            if (!await WebhookSignatureHelper.VerifyAsync(ctx, db))
                return Results.Unauthorized();

            using var reader = new System.IO.StreamReader(ctx.Request.Body);
            var raw = await reader.ReadToEndAsync();
            WhatsAppInboundDto? dto;
            try { dto = System.Text.Json.JsonSerializer.Deserialize<WhatsAppInboundDto>(raw, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { return Results.BadRequest(); }
            if (dto == null) return Results.BadRequest();

            var lead = await db.Leads.FirstOrDefaultAsync(l => l.Phone == dto.From);
            if (lead == null) return Results.Ok();

            var msg = new WhatsAppMessage
            {
                TenantId = lead.TenantId,
                LeadId = lead.Id,
                SentById = lead.AssignedToId ?? Guid.Empty,
                ToPhone = dto.From,
                Body = dto.Body,
                Direction = WhatsAppMessageDirection.Inbound,
                Status = WhatsAppMessageStatus.Delivered,
                ProviderMessageId = dto.MessageId,
                SentAt = DateTime.UtcNow
            };
            db.WhatsAppMessages.Add(msg);
            await db.SaveChangesAsync();
            return Results.Ok();
        }).AllowAnonymous();

        // Get all WhatsApp templates for tenant
        group.MapGet("/templates", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            // In production these come from WABA template list API
            var templates = new[]
            {
                new { id = "follow_up_1", name = "Follow Up Reminder", body = "Hi {{1}}, this is a reminder for your scheduled follow-up. Please call us at {{2}}." },
                new { id = "appointment_1", name = "Appointment Confirmation", body = "Dear {{1}}, your appointment is confirmed for {{2}}. See you then!" },
                new { id = "payment_1", name = "Payment Reminder", body = "Hi {{1}}, your payment of {{2}} is due on {{3}}. Please arrange accordingly." },
                new { id = "welcome_1", name = "Welcome Message", body = "Welcome {{1}}! Thank you for your interest. Our team will contact you shortly." }
            };
            return Results.Ok(templates);
        });
    }
}

public record SendWhatsAppDto(Guid LeadId, string ToPhone, string Body, string? TemplateId, string? MediaUrl);
public record WhatsAppInboundDto(string From, string Body, string? MessageId);
