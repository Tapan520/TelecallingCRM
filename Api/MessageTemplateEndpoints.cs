using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

/// <summary>
/// CRUD for SMS and WhatsApp templates, plus a render endpoint that
/// substitutes {{lead_name}} / {{agent_name}} / {{phone}} / {{company}} variables.
///
/// SMS:       GET/POST/PUT/DELETE /api/sms-templates
///            POST /api/sms-templates/{id}/render
/// WhatsApp:  GET/POST/PUT/DELETE /api/whatsapp-templates
///            POST /api/whatsapp-templates/{id}/render
/// </summary>
public static class MessageTemplateEndpoints
{
    // ?? Variables supported in all templates ?????????????????????????????????
    private static readonly string[] KnownVars =
        ["lead_name", "agent_name", "phone", "company", "date", "time"];

    public static void MapMessageTemplateEndpoints(this WebApplication app)
    {
        MapSmsTemplateEndpoints(app);
        MapWhatsAppTemplateEndpoints(app);
    }

    // ????????????????????????????????????????????????????????????????????????
    //  SMS Templates
    // ????????????????????????????????????????????????????????????????????????
    private static void MapSmsTemplateEndpoints(WebApplication app)
    {
        var grp = app.MapGroup("/api/sms-templates")
            .WithTags("SMS Templates")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        // LIST
        grp.MapGet("/", async (TenantContext tc, AppDbContext db,
            [FromQuery] string? category, [FromQuery] bool? active) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var q = db.SmsTemplates.Where(t => t.TenantId == tc.TenantId).AsQueryable();
            if (!string.IsNullOrWhiteSpace(category)) q = q.Where(t => t.Category == category);
            if (active.HasValue) q = q.Where(t => t.IsActive == active.Value);
            var items = await q.OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Name, t.Body, t.Category, t.IsActive, t.CreatedAt })
                .ToListAsync();
            return Results.Ok(items);
        });

        // GET single
        grp.MapGet("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var t = await db.SmsTemplates.FirstOrDefaultAsync(
                x => x.Id == id && x.TenantId == tc.TenantId);
            return t == null ? Results.NotFound() : Results.Ok(t);
        });

        // CREATE
        grp.MapPost("/", async ([FromBody] SmsTemplateUpsertDto dto,
            TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var tmpl = new SmsTemplate
            {
                TenantId = tc.TenantId,
                Name     = dto.Name,
                Body     = dto.Body,
                Category = dto.Category,
                IsActive = true
            };
            db.SmsTemplates.Add(tmpl);
            await db.SaveChangesAsync();
            return Results.Created($"/api/sms-templates/{tmpl.Id}", new { tmpl.Id });
        });

        // UPDATE
        grp.MapPut("/{id:guid}", async (Guid id, [FromBody] SmsTemplateUpsertDto dto,
            TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var tmpl = await db.SmsTemplates.FirstOrDefaultAsync(
                x => x.Id == id && x.TenantId == tc.TenantId);
            if (tmpl == null) return Results.NotFound();
            tmpl.Name = dto.Name; tmpl.Body = dto.Body; tmpl.Category = dto.Category;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // TOGGLE active
        grp.MapPost("/{id:guid}/toggle", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var tmpl = await db.SmsTemplates.FirstOrDefaultAsync(
                x => x.Id == id && x.TenantId == tc.TenantId);
            if (tmpl == null) return Results.NotFound();
            tmpl.IsActive = !tmpl.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(new { tmpl.IsActive });
        });

        // DELETE
        grp.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var tmpl = await db.SmsTemplates.FirstOrDefaultAsync(
                x => x.Id == id && x.TenantId == tc.TenantId);
            if (tmpl == null) return Results.NotFound();
            db.SmsTemplates.Remove(tmpl);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // RENDER – substitute variables and return rendered body
        grp.MapPost("/{id:guid}/render", async (Guid id,
            [FromBody] RenderTemplateDto dto,
            TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var tmpl = await db.SmsTemplates.FirstOrDefaultAsync(
                x => x.Id == id && x.TenantId == tc.TenantId);
            if (tmpl == null) return Results.NotFound();
            var rendered = RenderBody(tmpl.Body, dto.Variables);
            return Results.Ok(new { rendered, original = tmpl.Body });
        });
    }

    // ????????????????????????????????????????????????????????????????????????
    //  WhatsApp Templates
    // ????????????????????????????????????????????????????????????????????????
    private static void MapWhatsAppTemplateEndpoints(WebApplication app)
    {
        var grp = app.MapGroup("/api/whatsapp-templates")
            .WithTags("WhatsApp Templates")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        // LIST
        grp.MapGet("/", async (TenantContext tc, AppDbContext db,
            [FromQuery] string? category, [FromQuery] bool? active) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var q = db.WhatsAppTemplates.Where(t => t.TenantId == tc.TenantId).AsQueryable();
            if (!string.IsNullOrWhiteSpace(category)) q = q.Where(t => t.Category == category);
            if (active.HasValue) q = q.Where(t => t.IsActive == active.Value);
            var items = await q.OrderBy(t => t.Name)
                .Select(t => new {
                    t.Id, t.Name, t.TemplateName, t.Language,
                    t.BodyPreview, t.HeaderType, t.Footer,
                    t.Category, t.IsActive, t.CreatedAt
                })
                .ToListAsync();
            return Results.Ok(items);
        });

        // GET single
        grp.MapGet("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var t = await db.WhatsAppTemplates.FirstOrDefaultAsync(
                x => x.Id == id && x.TenantId == tc.TenantId);
            return t == null ? Results.NotFound() : Results.Ok(t);
        });

        // CREATE
        grp.MapPost("/", async ([FromBody] WhatsAppTemplateUpsertDto dto,
            TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var tmpl = new WhatsAppTemplate
            {
                TenantId     = tc.TenantId,
                Name         = dto.Name,
                TemplateName = dto.TemplateName,
                Language     = dto.Language ?? "en",
                BodyPreview  = dto.BodyPreview,
                HeaderType   = dto.HeaderType,
                HeaderValue  = dto.HeaderValue,
                Footer       = dto.Footer,
                ButtonsJson  = dto.ButtonsJson,
                Category     = dto.Category,
                IsActive     = true
            };
            db.WhatsAppTemplates.Add(tmpl);
            await db.SaveChangesAsync();
            return Results.Created($"/api/whatsapp-templates/{tmpl.Id}", new { tmpl.Id });
        });

        // UPDATE
        grp.MapPut("/{id:guid}", async (Guid id, [FromBody] WhatsAppTemplateUpsertDto dto,
            TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var tmpl = await db.WhatsAppTemplates.FirstOrDefaultAsync(
                x => x.Id == id && x.TenantId == tc.TenantId);
            if (tmpl == null) return Results.NotFound();
            tmpl.Name         = dto.Name;
            tmpl.TemplateName = dto.TemplateName;
            tmpl.Language     = dto.Language ?? "en";
            tmpl.BodyPreview  = dto.BodyPreview;
            tmpl.HeaderType   = dto.HeaderType;
            tmpl.HeaderValue  = dto.HeaderValue;
            tmpl.Footer       = dto.Footer;
            tmpl.ButtonsJson  = dto.ButtonsJson;
            tmpl.Category     = dto.Category;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // TOGGLE active
        grp.MapPost("/{id:guid}/toggle", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var tmpl = await db.WhatsAppTemplates.FirstOrDefaultAsync(
                x => x.Id == id && x.TenantId == tc.TenantId);
            if (tmpl == null) return Results.NotFound();
            tmpl.IsActive = !tmpl.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(new { tmpl.IsActive });
        });

        // DELETE
        grp.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var tmpl = await db.WhatsAppTemplates.FirstOrDefaultAsync(
                x => x.Id == id && x.TenantId == tc.TenantId);
            if (tmpl == null) return Results.NotFound();
            db.WhatsAppTemplates.Remove(tmpl);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // RENDER – returns the body with {{1}}, {{2}} replaced by caller-supplied values
        grp.MapPost("/{id:guid}/render", async (Guid id,
            [FromBody] RenderTemplateDto dto,
            TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var tmpl = await db.WhatsAppTemplates.FirstOrDefaultAsync(
                x => x.Id == id && x.TenantId == tc.TenantId);
            if (tmpl == null) return Results.NotFound();
            var rendered = RenderBody(tmpl.BodyPreview, dto.Variables);
            return Results.Ok(new { rendered, original = tmpl.BodyPreview });
        });
    }

    // ?? Shared variable renderer ??????????????????????????????????????????????
    /// <summary>
    /// Replaces {{key}} placeholders in the body with values from the dictionary.
    /// Keys are case-insensitive. Also replaces positional {{1}}, {{2}} markers
    /// used by WABA templates.
    /// </summary>
    private static string RenderBody(string body, Dictionary<string, string>? variables)
    {
        if (string.IsNullOrEmpty(body) || variables == null) return body;
        var result = body;
        foreach (var (key, value) in variables)
            result = result.Replace($"{{{{{key}}}}}", value,
                StringComparison.OrdinalIgnoreCase);
        return result;
    }
}

// ?? DTOs ?????????????????????????????????????????????????????????????????????

public record SmsTemplateUpsertDto(string Name, string Body, string? Category);

public record WhatsAppTemplateUpsertDto(
    string Name,
    string TemplateName,
    string BodyPreview,
    string? Language,
    string? HeaderType,
    string? HeaderValue,
    string? Footer,
    string? ButtonsJson,
    string? Category);

public record RenderTemplateDto(Dictionary<string, string>? Variables);
