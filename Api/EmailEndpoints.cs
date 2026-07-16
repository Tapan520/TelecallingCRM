using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class EmailEndpoints
{
    public static void MapEmailEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/email").WithTags("Email").RequireAuthorization().RequireRateLimiting("api");

        // Send email
        group.MapPost("/send", async ([FromBody] SendEmailDto dto, TenantContext tc,
            AppDbContext db, HttpContext http, IMessageDispatcher dispatcher) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var trackingToken = Guid.NewGuid().ToString("N");
            // Inject open-tracking pixel — derive base URL from current request
            var baseUrl = $"{http.Request.Scheme}://{http.Request.Host}";
            var trackedBody = dto.Body + $"<img src='{baseUrl}/api/email/track/{trackingToken}' width='1' height='1' style='display:none' />";

            var msg = new EmailMessage
            {
                TenantId = tc.TenantId,
                LeadId = dto.LeadId,
                SentById = userId,
                ToEmail = dto.ToEmail,
                Subject = dto.Subject,
                Body = trackedBody,
                IsHtml = dto.IsHtml,
                TemplateId = dto.TemplateId,
                TrackingToken = trackingToken,
                Status = EmailStatus.Queued
            };
            db.EmailMessages.Add(msg);

            if (dto.LeadId.HasValue)
                db.ActivityLogs.Add(new ActivityLog {
                    TenantId = tc.TenantId, LeadId = dto.LeadId.Value, UserId = userId,
                    Type = ActivityType.EmailSent,
                    Summary = $"Email sent to {dto.ToEmail}: {dto.Subject}"
                });

            await db.SaveChangesAsync();

            var (success, error) = await dispatcher.SendEmailAsync(tc.TenantId, dto.ToEmail, dto.Subject, trackedBody);
            msg.Status = success ? EmailStatus.Sent : EmailStatus.Failed;
            msg.ErrorMessage = error;
            msg.SentAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return success ? Results.Ok(new { msg.Id, msg.Status, trackingToken })
                           : Results.Ok(new { msg.Id, msg.Status, warning = error, trackingToken });
        });

        // Open tracking pixel
        group.MapGet("/track/{token}", async (string token, AppDbContext db) =>
        {
            var msg = await db.EmailMessages.FirstOrDefaultAsync(e => e.TrackingToken == token);
            if (msg != null && msg.Status == EmailStatus.Sent)
            {
                msg.Status = EmailStatus.Opened;
                msg.OpenedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            // Return 1x1 transparent GIF
            var gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
            return Results.File(gif, "image/gif");
        }).AllowAnonymous();

        // Email history for a lead
        group.MapGet("/lead/{leadId:guid}", async (Guid leadId, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var msgs = await db.EmailMessages
                .Where(e => e.LeadId == leadId && e.TenantId == tc.TenantId)
                .Include(e => e.SentBy)
                .OrderByDescending(e => e.SentAt)
                .Select(e => new { e.Id, e.Subject, e.ToEmail, e.Status, e.SentAt, e.OpenedAt, SentBy = e.SentBy.FullName })
                .ToListAsync();
            return Results.Ok(msgs);
        });

        // Templates CRUD
        group.MapGet("/templates", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            return Results.Ok(await db.EmailTemplates
                .Where(t => t.TenantId == tc.TenantId)
                .Select(t => new { t.Id, t.Name, t.Subject, t.Category, t.CreatedAt })
                .ToListAsync());
        });

        group.MapPost("/templates", async ([FromBody] EmailTemplateDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var tmpl = new EmailTemplate {
                TenantId = tc.TenantId, Name = dto.Name,
                Subject = dto.Subject, Body = dto.Body, Category = dto.Category
            };
            db.EmailTemplates.Add(tmpl);
            await db.SaveChangesAsync();
            return Results.Created($"/api/email/templates/{tmpl.Id}", tmpl);
        });

        group.MapPut("/templates/{id:guid}", async (Guid id, [FromBody] EmailTemplateDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var tmpl = await db.EmailTemplates.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tc.TenantId);
            if (tmpl == null) return Results.NotFound();
            tmpl.Name = dto.Name; tmpl.Subject = dto.Subject; tmpl.Body = dto.Body; tmpl.Category = dto.Category;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        group.MapDelete("/templates/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var tmpl = await db.EmailTemplates.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tc.TenantId);
            if (tmpl == null) return Results.NotFound();
            db.EmailTemplates.Remove(tmpl);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}

public record SendEmailDto(string ToEmail, string Subject, string Body, bool IsHtml, Guid? LeadId, string? TemplateId);
public record EmailTemplateDto(string Name, string Subject, string Body, string? Category);
