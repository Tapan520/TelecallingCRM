using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class InboxEndpoints
{
    public static void MapInboxEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/inbox")
            .WithTags("Inbox")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        // GET unified inbox: all messages (WhatsApp + SMS + Email) for the tenant
        group.MapGet("/", async (TenantContext tc, AppDbContext db,
            [FromQuery] string? channel, [FromQuery] Guid? leadId,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 30) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var messages = new List<object>();

            if (channel == null || channel == "whatsapp")
            {
                var wa = await db.WhatsAppMessages
                    .Where(m => m.TenantId == tc.TenantId && (leadId == null || m.LeadId == leadId))
                    .OrderByDescending(m => m.SentAt)
                    .Take(pageSize * 2)
                    .Select(m => new {
                        id = m.Id.ToString(), channel = "whatsapp",
                        leadName = m.Lead.Name, m.LeadId,
                        phone = m.ToPhone, body = m.Body,
                        direction = m.Direction.ToString(),
                        status = m.Status.ToString(),
                        sentAt = m.SentAt
                    }).ToListAsync();
                messages.AddRange(wa.Cast<object>());
            }

            if (channel == null || channel == "sms")
            {
                var sms = await db.SmsMessages
                    .Where(m => m.TenantId == tc.TenantId && (leadId == null || m.LeadId == leadId))
                    .OrderByDescending(m => m.SentAt)
                    .Take(pageSize * 2)
                    .Select(m => new {
                        id = m.Id.ToString(), channel = "sms",
                        leadName = m.Lead != null ? m.Lead.Name : "Unknown", m.LeadId,
                        phone = m.ToPhone, body = m.Body,
                        direction = "outbound",
                        status = m.Status.ToString(),
                        sentAt = m.SentAt
                    }).ToListAsync();
                messages.AddRange(sms.Cast<object>());
            }

            if (channel == null || channel == "email")
            {
                var emails = await db.EmailMessages
                    .Where(m => m.TenantId == tc.TenantId && (leadId == null || m.LeadId == leadId))
                    .OrderByDescending(m => m.SentAt)
                    .Take(pageSize * 2)
                    .Select(m => new {
                        id = m.Id.ToString(), channel = "email",
                        leadName = m.Lead != null ? m.Lead.Name : "Unknown", m.LeadId,
                        phone = m.ToEmail, body = m.Subject,
                        direction = "outbound",
                        status = m.Status.ToString(),
                        sentAt = m.SentAt
                    }).ToListAsync();
                messages.AddRange(emails.Cast<object>());
            }

            // Sort all merged messages by date descending
            var sorted = messages
                .OrderByDescending(m => (DateTime)((dynamic)m).sentAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Results.Ok(new { page, pageSize, items = sorted });
        });

        // GET lead conversation thread (all channels)
        group.MapGet("/thread/{leadId:guid}", async (Guid leadId, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var wa = await db.WhatsAppMessages
                .Where(m => m.TenantId == tc.TenantId && m.LeadId == leadId)
                .Select(m => new { id = m.Id.ToString(), ch = "whatsapp", m.Body, dir = m.Direction.ToString(), ts = m.SentAt, status = m.Status.ToString() })
                .ToListAsync();
            var sms = await db.SmsMessages
                .Where(m => m.TenantId == tc.TenantId && m.LeadId == leadId)
                .Select(m => new { id = m.Id.ToString(), ch = "sms", m.Body, dir = "outbound", ts = m.SentAt, status = m.Status.ToString() })
                .ToListAsync();
            var emails = await db.EmailMessages
                .Where(m => m.TenantId == tc.TenantId && m.LeadId == leadId)
                .Select(m => new { id = m.Id.ToString(), ch = "email", m.Body, dir = "outbound", ts = m.SentAt, status = m.Status.ToString() })
                .ToListAsync();

            var all = wa.Cast<dynamic>().Concat(sms.Cast<dynamic>()).Concat(emails.Cast<dynamic>())
                .OrderBy(m => (DateTime)m.ts).ToList();

            return Results.Ok(all);
        });
    }
}
