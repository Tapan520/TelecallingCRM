using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class IntegrationsEndpoints
{
    // All supported providers with their required config fields
    public static readonly Dictionary<string, string[]> SupportedProviders = new()
    {
        ["twilio"]         = ["AccountSid", "AuthToken", "FromNumber"],
        ["exotel"]         = ["ApiKey", "ApiToken", "AccountSid", "FromNumber"],
        ["knowlarity"]     = ["ApiKey", "CallerId", "SRNumber"],
        ["airteliq"]       = ["ClientId", "ClientSecret", "CallerId"],
        ["razorpay"]       = ["KeyId", "KeySecret", "WebhookSecret"],
        ["whatsapp_waba"]  = ["PhoneNumberId", "AccessToken", "VerifyToken"],
        ["smtp"]           = ["Host", "Port", "Username", "Password", "FromEmail", "FromName"],
        ["sendgrid"]       = ["ApiKey", "FromEmail", "FromName"],
        ["google_calendar"]= ["ClientId", "ClientSecret", "RefreshToken"],
        ["facebook_leads"] = ["PageAccessToken", "AppSecret"],
    };

    public static void MapIntegrationsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/integrations").WithTags("Integrations")
            .RequireAuthorization(p => p.RequireRole("admin", "superadmin"))
            .RequireRateLimiting("api");

        // Get all integration configs for tenant
        group.MapGet("/", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var configs = await db.IntegrationConfigs
                .Where(i => i.TenantId == tc.TenantId)
                .Select(i => new { i.Id, i.Provider, i.IsEnabled, i.UpdatedAt })
                .ToListAsync();

            // Return all supported providers, marking which are configured
            var result = SupportedProviders.Keys.Select(p =>
            {
                var cfg = configs.FirstOrDefault(c => c.Provider == p);
                return new { provider = p, isConfigured = cfg != null, isEnabled = cfg?.IsEnabled ?? false, updatedAt = cfg?.UpdatedAt };
            });
            return Results.Ok(result);
        });

        // Save / update integration config
        group.MapPost("/{provider}", async (string provider, [FromBody] Dictionary<string, string> configValues,
            TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            if (!SupportedProviders.ContainsKey(provider))
                return Results.BadRequest($"Unknown provider '{provider}'.");

            var existing = await db.IntegrationConfigs
                .FirstOrDefaultAsync(i => i.TenantId == tc.TenantId && i.Provider == provider);

            if (existing == null)
            {
                existing = new IntegrationConfig { TenantId = tc.TenantId, Provider = provider };
                db.IntegrationConfigs.Add(existing);
            }
            existing.ConfigJson = System.Text.Json.JsonSerializer.Serialize(configValues);
            existing.IsEnabled = true;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { provider, isEnabled = true });
        });

        // Toggle enable/disable
        group.MapPost("/{provider}/toggle", async (string provider, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var cfg = await db.IntegrationConfigs
                .FirstOrDefaultAsync(i => i.TenantId == tc.TenantId && i.Provider == provider);
            if (cfg == null) return Results.NotFound();
            cfg.IsEnabled = !cfg.IsEnabled;
            await db.SaveChangesAsync();
            return Results.Ok(new { cfg.IsEnabled });
        });

        // Get required config fields for a provider
        group.MapGet("/{provider}/fields", (string provider) =>
        {
            if (!SupportedProviders.TryGetValue(provider, out var fields))
                return Results.BadRequest($"Unknown provider '{provider}'.");
            return Results.Ok(fields);
        });
    }
}
