using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class SmsEndpoints
{
    public static void MapSmsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sms").WithTags("SMS").RequireAuthorization().RequireRateLimiting("api");

        group.MapPost("/send", async ([FromBody] SendSmsDto dto, TenantContext tc,
            AppDbContext db, HttpContext http, IMessageDispatcher dispatcher) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var msg = new SmsMessage
            {
                TenantId = tc.TenantId,
                LeadId = dto.LeadId,
                SentById = userId,
                ToPhone = dto.ToPhone,
                Body = dto.Body,
                Type = dto.Type,
                Status = SmsMessageStatus.Queued
            };
            db.SmsMessages.Add(msg);

            if (dto.LeadId.HasValue)
                db.ActivityLogs.Add(new ActivityLog {
                    TenantId = tc.TenantId, LeadId = dto.LeadId.Value, UserId = userId,
                    Type = ActivityType.SmsSent,
                    Summary = $"SMS sent to {dto.ToPhone}: {dto.Body.Substring(0, Math.Min(60, dto.Body.Length))}..."
                });

            await db.SaveChangesAsync();

            var (success, error) = await dispatcher.SendSmsAsync(tc.TenantId, dto.ToPhone, dto.Body);
            msg.Status = success ? SmsMessageStatus.Sent : SmsMessageStatus.Failed;
            msg.ErrorMessage = error;
            await db.SaveChangesAsync();

            return success ? Results.Ok(new { msg.Id, msg.Status })
                           : Results.Ok(new { msg.Id, msg.Status, warning = error });
        });

        // Bulk SMS to all leads in a campaign
        group.MapPost("/bulk", async ([FromBody] BulkSmsDto dto, TenantContext tc,
            AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var leads = await db.Leads
                .Where(l => l.TenantId == tc.TenantId && dto.LeadIds.Contains(l.Id))
                .ToListAsync();

            var messages = leads.Select(lead => new SmsMessage {
                TenantId = tc.TenantId, LeadId = lead.Id, SentById = userId,
                ToPhone = lead.Phone, Body = dto.Body,
                Type = SmsMessageType.Bulk, Status = SmsMessageStatus.Queued
            }).ToList();

            db.SmsMessages.AddRange(messages);
            await db.SaveChangesAsync();

            return Results.Ok(new { queued = messages.Count });
        });

        // Get SMS history for a lead
        group.MapGet("/lead/{leadId:guid}", async (Guid leadId, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var msgs = await db.SmsMessages
                .Where(s => s.LeadId == leadId && s.TenantId == tc.TenantId)
                .Include(s => s.SentBy)
                .OrderByDescending(s => s.SentAt)
                .Select(s => new { s.Id, s.Body, s.Type, s.Status, s.SentAt, s.DeliveredAt, SentBy = s.SentBy.FullName })
                .ToListAsync();
            return Results.Ok(msgs);
        });

        // GET /api/sms/log — recent SMS across the whole tenant
        group.MapGet("/log", async (TenantContext tc, AppDbContext db,
            [FromQuery] int pageSize = 30) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var msgs = await db.SmsMessages
                .Where(s => s.TenantId == tc.TenantId)
                .Include(s => s.SentBy)
                .OrderByDescending(s => s.SentAt)
                .Take(pageSize)
                .Select(s => new {
                    s.Id, s.ToPhone, s.Body, s.Type, s.Status,
                    s.SentAt, s.DeliveredAt,
                    SentBy = s.SentBy != null ? s.SentBy.FullName : "System"
                })
                .ToListAsync();
            return Results.Ok(msgs);
        });

        // Delivery report webhook
        group.MapPost("/webhook/delivery", async ([FromBody] SmsDeliveryDto dto, AppDbContext db) =>
        {
            var msg = await db.SmsMessages
                .FirstOrDefaultAsync(s => s.ProviderMessageId == dto.MessageId);
            if (msg != null)
            {
                msg.Status = dto.Status == "delivered" ? SmsMessageStatus.Delivered : SmsMessageStatus.Failed;
                if (msg.Status == SmsMessageStatus.Delivered) msg.DeliveredAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            return Results.Ok();
        }).AllowAnonymous();
    }
}

public record SendSmsDto(string ToPhone, string Body, SmsMessageType Type, Guid? LeadId);
public record BulkSmsDto(string Body, List<Guid> LeadIds);
public record SmsDeliveryDto(string MessageId, string Status);
