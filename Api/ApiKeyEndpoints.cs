using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class ApiKeyEndpoints
{
    public static void MapApiKeyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/api-keys")
            .WithTags("ApiKeys")
            .RequireAuthorization(p => p.RequireRole("admin", "superadmin"))
            .RequireRateLimiting("api");

        group.MapGet("/", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var keys = await db.ApiKeys
                .Where(k => k.TenantId == tc.TenantId)
                .Include(k => k.CreatedBy)
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => new {
                    k.Id, k.Name, k.KeyPrefix, k.Scopes, k.IsActive,
                    k.CreatedAt, k.LastUsedAt, k.ExpiresAt,
                    CreatedBy = k.CreatedBy.FullName
                })
                .ToListAsync();
            return Results.Ok(keys);
        });

        group.MapPost("/", async ([FromBody] CreateApiKeyDto dto, TenantContext tc,
            AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            // Generate a secure random key
            var rawKey = "tcrm_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace("+", "").Replace("/", "").Replace("=", "").Substring(0, 40);

            using var sha = SHA256.Create();
            var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(rawKey))).ToLower();

            var apiKey = new ApiKey
            {
                TenantId = tc.TenantId,
                CreatedById = userId,
                Name = dto.Name,
                KeyHash = hash,
                KeyPrefix = rawKey[..8],
                Scopes = dto.Scopes,
                ExpiresAt = dto.ExpiresAt
            };
            db.ApiKeys.Add(apiKey);
            await db.SaveChangesAsync();

            // Return raw key only once
            return Results.Created($"/api/api-keys/{apiKey.Id}", new {
                apiKey.Id, apiKey.Name, apiKey.KeyPrefix, apiKey.Scopes,
                rawKey, // shown only on creation
                message = "Save this key now — it will not be shown again."
            });
        });

        group.MapPost("/{id:guid}/toggle", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id && k.TenantId == tc.TenantId);
            if (key == null) return Results.NotFound();
            key.IsActive = !key.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(new { key.IsActive });
        });

        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id && k.TenantId == tc.TenantId);
            if (key == null) return Results.NotFound();
            db.ApiKeys.Remove(key);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}

public record CreateApiKeyDto(string Name, string Scopes, DateTime? ExpiresAt);
