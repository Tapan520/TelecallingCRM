using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class CustomLeadFieldEndpoints
{
    public static void MapCustomLeadFieldEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/custom-fields")
            .WithTags("CustomFields")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapGet("/", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var fields = await db.CustomLeadFields
                .Where(f => f.TenantId == tc.TenantId && f.IsActive)
                .OrderBy(f => f.SortOrder).ThenBy(f => f.Label)
                .Select(f => new { f.Id, f.Name, f.Label, f.FieldType, f.Options, f.IsRequired, f.SortOrder })
                .ToListAsync();
            return Results.Ok(fields);
        });

        group.MapPost("/", async ([FromBody] CustomFieldUpsertDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var field = new CustomLeadField
            {
                TenantId = tc.TenantId,
                Name = dto.Name.ToLower().Replace(" ", "_"),
                Label = dto.Label,
                FieldType = dto.FieldType,
                Options = dto.Options,
                IsRequired = dto.IsRequired,
                SortOrder = dto.SortOrder
            };
            db.CustomLeadFields.Add(field);
            await db.SaveChangesAsync();
            return Results.Created($"/api/custom-fields/{field.Id}", field);
        }).RequireAuthorization(p => p.RequireRole("admin", "superadmin"));

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] CustomFieldUpsertDto dto,
            TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var field = await db.CustomLeadFields.FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tc.TenantId);
            if (field == null) return Results.NotFound();
            field.Label = dto.Label;
            field.FieldType = dto.FieldType;
            field.Options = dto.Options;
            field.IsRequired = dto.IsRequired;
            field.SortOrder = dto.SortOrder;
            await db.SaveChangesAsync();
            return Results.Ok(field);
        }).RequireAuthorization(p => p.RequireRole("admin", "superadmin"));

        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var field = await db.CustomLeadFields.FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tc.TenantId);
            if (field == null) return Results.NotFound();
            field.IsActive = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(p => p.RequireRole("admin", "superadmin"));
    }
}

public record CustomFieldUpsertDto(string Name, string Label, string FieldType,
    string? Options, bool IsRequired, int SortOrder);
