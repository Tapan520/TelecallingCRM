using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class GlobalSearchEndpoints
{
    public static void MapGlobalSearchEndpoints(this WebApplication app)
    {
        app.MapGet("/api/search", async (
            [FromQuery] string q,
            TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2) return Results.BadRequest("Query too short.");
            var tid = tc.TenantId;
            var term = q.Trim();

            var leads = await db.Leads
                .Where(l => l.TenantId == tid && (l.Name.Contains(term) || l.Phone.Contains(term) || (l.Email != null && l.Email.Contains(term)) || (l.Company != null && l.Company.Contains(term))))
                .OrderByDescending(l => l.UpdatedAt)
                .Take(5)
                .Select(l => new SearchResult("lead", l.Id.ToString(), l.Name, l.Phone, $"/Leads/Detail?id={l.Id}"))
                .ToListAsync();

            var contacts = await db.Calls
                .Where(c => c.TenantId == tid && (c.Notes != null && c.Notes.Contains(term)))
                .OrderByDescending(c => c.StartedAt)
                .Take(3)
                .Select(c => new SearchResult("call", c.Id.ToString(), $"Call to {c.Lead.Name}", c.StartedAt.ToString("d"), $"/Calls/Index"))
                .ToListAsync();

            var deals = await db.Deals
                .Where(d => d.TenantId == tid && d.Title.Contains(term))
                .Take(5)
                .Select(d => new SearchResult("deal", d.Id.ToString(), d.Title, d.Stage.ToString(), $"/Deals/Index"))
                .ToListAsync();

            var campaigns = await db.Campaigns
                .Where(c => c.TenantId == tid && c.Name.Contains(term))
                .Take(3)
                .Select(c => new SearchResult("campaign", c.Id.ToString(), c.Name, c.Status.ToString(), $"/Campaigns/Index"))
                .ToListAsync();

            var tasks = await db.Tasks
                .Where(t => t.TenantId == tid && t.Title.Contains(term))
                .Take(3)
                .Select(t => new SearchResult("task", t.Id.ToString(), t.Title, t.Status.ToString(), $"/Tasks/Index"))
                .ToListAsync();

            var quotes = await db.Quotes
                .Where(qt => qt.TenantId == tid && (qt.QuoteNumber.Contains(term) || (qt.Title != null && qt.Title.Contains(term))))
                .Take(3)
                .Select(qt => new SearchResult("quote", qt.Id.ToString(), qt.Title ?? qt.QuoteNumber, qt.Status.ToString(), $"/Quotes/Index"))
                .ToListAsync();

            var results = leads.Concat(deals).Concat(campaigns).Concat(tasks).Concat(quotes).Concat(contacts).ToList();
            return Results.Ok(new { query = q, count = results.Count, results });
        }).RequireAuthorization().RequireRateLimiting("api").WithTags("Search");
    }
}

public record SearchResult(string Type, string Id, string Title, string? Subtitle, string Url);
