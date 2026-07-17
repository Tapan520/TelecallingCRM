using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class DispositionFormEndpoints
{
    public static void MapDispositionFormEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/disposition-forms").WithTags("DispositionForms").RequireAuthorization().RequireRateLimiting("api");

        // ?? Forms ?????????????????????????????????????????????????????????????
        group.MapGet("/", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var forms = await db.DispositionForms
                .Where(f => f.TenantId == tc.TenantId)
                .Include(f => f.Fields.OrderBy(x => x.SortOrder))
                .Select(f => new {
                    f.Id, f.Name, f.IsDefault, f.IsActive, f.CampaignId, f.CreatedAt,
                    FieldCount = f.Fields.Count,
                    CampaignName = f.Campaign != null ? f.Campaign.Name : null
                })
                .ToListAsync();
            return Results.Ok(forms);
        });

        group.MapGet("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var form = await db.DispositionForms
                .Include(f => f.Fields.OrderBy(x => x.SortOrder))
                .FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tc.TenantId);
            if (form is null) return Results.NotFound();
            return Results.Ok(new {
                form.Id, form.Name, form.IsDefault, form.IsActive,
                form.CampaignId, form.CreatedAt,
                fields = form.Fields.Select(f => new {
                    f.Id, f.Label, f.FieldType, f.Options, f.IsRequired, f.SortOrder
                }).ToList()
            });
        });

        group.MapPost("/", async ([FromBody] DispositionFormDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            if (dto.IsDefault)
                await db.DispositionForms
                    .Where(f => f.TenantId == tc.TenantId && f.IsDefault)
                    .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsDefault, false));
            var form = new DispositionForm
            {
                TenantId = tc.TenantId,
                Name = dto.Name, IsDefault = dto.IsDefault,
                IsActive = dto.IsActive, CampaignId = dto.CampaignId
            };
            foreach (var (fld, i) in dto.Fields.Select((f, i) => (f, i)))
                form.Fields.Add(new DispositionField {
                    Label = fld.Label, FieldType = fld.FieldType,
                    Options = fld.Options, IsRequired = fld.IsRequired, SortOrder = i
                });
            db.DispositionForms.Add(form);
            await db.SaveChangesAsync();
            return Results.Created($"/api/disposition-forms/{form.Id}", new { form.Id, form.Name });
        });

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] DispositionFormDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var form = await db.DispositionForms.Include(f => f.Fields)
                .FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tc.TenantId);
            if (form is null) return Results.NotFound();
            if (dto.IsDefault && !form.IsDefault)
                await db.DispositionForms
                    .Where(f => f.TenantId == tc.TenantId && f.IsDefault)
                    .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsDefault, false));
            form.Name = dto.Name; form.IsDefault = dto.IsDefault;
            form.IsActive = dto.IsActive; form.CampaignId = dto.CampaignId;
            db.DispositionFields.RemoveRange(form.Fields);
            foreach (var (fld, i) in dto.Fields.Select((f, i) => (f, i)))
                form.Fields.Add(new DispositionField {
                    Label = fld.Label, FieldType = fld.FieldType,
                    Options = fld.Options, IsRequired = fld.IsRequired, SortOrder = i
                });
            await db.SaveChangesAsync();
            return Results.Ok(new { form.Id });
        });

        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var form = await db.DispositionForms.FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tc.TenantId);
            if (form is null) return Results.NotFound();
            db.DispositionForms.Remove(form);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ?? Responses ?????????????????????????????????????????????????????????
        group.MapPost("/{id:guid}/responses", async (Guid id, [FromBody] DispositionResponseDto dto, TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var agentId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var resp = new DispositionResponse
            {
                TenantId = tc.TenantId,
                FormId = id, CallId = dto.CallId,
                AgentId = agentId, LeadId = dto.LeadId,
                AnswersJson = dto.AnswersJson
            };
            db.DispositionResponses.Add(resp);
            await db.SaveChangesAsync();
            return Results.Created($"/api/disposition-forms/{id}/responses/{resp.Id}", new { resp.Id });
        });

        group.MapGet("/{id:guid}/responses", async (Guid id, TenantContext tc, AppDbContext db,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var q = db.DispositionResponses.Where(r => r.FormId == id && r.TenantId == tc.TenantId);
            var total = await q.CountAsync();
            var items = await q.OrderByDescending(r => r.SubmittedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(r => new {
                    r.Id, r.CallId, r.LeadId, r.AnswersJson, r.SubmittedAt,
                    AgentName = r.Agent.FullName, LeadName = r.Lead.Name
                }).ToListAsync();
            return Results.Ok(new { total, items });
        });
    }
}

public record DispositionFormDto(string Name, bool IsDefault, bool IsActive, Guid? CampaignId, List<DispositionFieldDto> Fields);
public record DispositionFieldDto(string Label, DispositionFieldType FieldType, string? Options, bool IsRequired);
public record DispositionResponseDto(Guid CallId, Guid LeadId, string AnswersJson);
