using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

/// <summary>
/// Do-Not-Call list management.
///
/// GET    /api/dnc              – list all DNC entries (paginated)
/// POST   /api/dnc              – add a single number
/// DELETE /api/dnc/{id}         – remove a number
/// POST   /api/dnc/import       – bulk CSV import (one phone per line or Name,Phone)
/// GET    /api/dnc/check/{phone} – check if a phone is on the DNC list
/// </summary>
public static class DncEndpoints
{
    public static void MapDncEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dnc")
            .WithTags("DNC")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        // ?? LIST ??????????????????????????????????????????????????????????????
        group.MapGet("/", async (TenantContext tc, AppDbContext db,
            [FromQuery] string? q,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var query = db.DncEntries
                .Where(d => d.TenantId == tc.TenantId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(d => d.Phone.Contains(q));

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(d => d.AddedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new {
                    d.Id, d.Phone, d.Reason, d.AddedAt,
                    AddedBy = d.AddedBy.FullName
                })
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, items });
        });

        // ?? CHECK (used by call-start & lead-creation guard) ??????????????????
        group.MapGet("/check/{phone}", async (string phone, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var normalised = NormalisePhone(phone);
            var entry = await db.DncEntries
                .Where(d => d.TenantId == tc.TenantId && d.Phone == normalised)
                .Select(d => new { d.Id, d.Phone, d.Reason, d.AddedAt })
                .FirstOrDefaultAsync();
            return Results.Ok(new { isDnc = entry != null, entry });
        });

        // ?? ADD SINGLE ????????????????????????????????????????????????????????
        group.MapPost("/", async ([FromBody] DncAddDto dto,
            TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(
                http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var normalised = NormalisePhone(dto.Phone);
            if (string.IsNullOrWhiteSpace(normalised))
                return Results.BadRequest("Invalid phone number.");

            var exists = await db.DncEntries
                .AnyAsync(d => d.TenantId == tc.TenantId && d.Phone == normalised);
            if (exists)
                return Results.Conflict(new { message = $"{normalised} is already on the DNC list." });

            var entry = new DncEntry
            {
                TenantId  = tc.TenantId,
                Phone     = normalised,
                Reason    = dto.Reason,
                AddedById = userId
            };
            db.DncEntries.Add(entry);
            await db.SaveChangesAsync();
            return Results.Created($"/api/dnc/{entry.Id}", new { entry.Id, entry.Phone });
        });

        // ?? DELETE ????????????????????????????????????????????????????????????
        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var entry = await db.DncEntries
                .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tc.TenantId);
            if (entry == null) return Results.NotFound();
            db.DncEntries.Remove(entry);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ?? BULK CSV IMPORT ???????????????????????????????????????????????????
        // Accepts a plain-text file: one entry per line.
        // Supported formats per line:
        //   9876543210
        //   "John Doe","9876543210","Requested opt-out"
        group.MapPost("/import", async (IFormFile file,
            TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            if (file == null || file.Length == 0)
                return Results.BadRequest("No file uploaded.");

            var userId = Guid.Parse(
                http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var added   = 0;
            var skipped = 0;

            // Pre-load existing DNC phones for this tenant to avoid per-row queries
            var existingPhones = (await db.DncEntries
                .Where(d => d.TenantId == tc.TenantId)
                .Select(d => d.Phone)
                .ToListAsync()).ToHashSet();

            using var reader = new System.IO.StreamReader(file.OpenReadStream());
            while (!reader.EndOfStream)
            {
                var line = (await reader.ReadLineAsync() ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Try CSV parse for "name","phone","reason" or just a bare phone
                var cols = ParseCsvLine(line);
                string rawPhone;
                string? reason = null;

                if (cols.Length == 1)
                {
                    rawPhone = cols[0].Trim();
                }
                else if (cols.Length >= 2)
                {
                    // Column order: Phone  OR  Name, Phone  OR  Name, Phone, Reason
                    // If col[0] looks like a phone use it, otherwise assume col[1] is phone
                    rawPhone = LooksLikePhone(cols[0]) ? cols[0].Trim() : cols[1].Trim();
                    reason   = cols.Length >= 3 ? cols[2].Trim() : null;
                }
                else { skipped++; continue; }

                var normalised = NormalisePhone(rawPhone);
                if (string.IsNullOrWhiteSpace(normalised)) { skipped++; continue; }
                if (existingPhones.Contains(normalised))  { skipped++; continue; }

                db.DncEntries.Add(new DncEntry
                {
                    TenantId  = tc.TenantId,
                    Phone     = normalised,
                    Reason    = string.IsNullOrWhiteSpace(reason) ? "CSV import" : reason,
                    AddedById = userId
                });
                existingPhones.Add(normalised);
                added++;
            }

            if (added > 0) await db.SaveChangesAsync();
            return Results.Ok(new { added, skipped });
        })
        .RequireAuthorization(p => p.RequireRole("admin", "manager", "superadmin"))
        .DisableAntiforgery();
    }

    // ?? Helpers ???????????????????????????????????????????????????????????????

    /// <summary>Strips all non-digit characters for consistent storage and lookup.</summary>
    public static string NormalisePhone(string phone) =>
        new string(phone.Where(char.IsDigit).ToArray());

    private static bool LooksLikePhone(string s)
    {
        var digits = s.Count(char.IsDigit);
        return digits >= 7 && digits == s.Replace("+", "").Replace("-", "").Replace(" ", "").Length;
    }

    private static string[] ParseCsvLine(string line)
    {
        var result  = new List<string>();
        var inQuote = false;
        var current = new System.Text.StringBuilder();
        foreach (var ch in line)
        {
            if (ch == '"')        { inQuote = !inQuote; }
            else if (ch == ',' && !inQuote) { result.Add(current.ToString()); current.Clear(); }
            else                  { current.Append(ch); }
        }
        result.Add(current.ToString());
        return result.ToArray();
    }
}

public record DncAddDto(string Phone, string? Reason);
