using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Hubs;
using TelecallingCRM.Services;
using System.Security.Claims;

namespace TelecallingCRM.Api;

public static class LeadsEndpoints
{
    public static void MapLeadsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/leads").WithTags("Leads").RequireAuthorization().RequireRateLimiting("api");

        group.MapGet("/", async (TenantContext tc, AppDbContext db,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
            [FromQuery] string? status = null, [FromQuery] string? q = null) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var query = db.Leads
                .Where(l => l.TenantId == tc.TenantId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<LeadStatus>(status, true, out var ls))
                query = query.Where(l => l.Status == ls);

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(l => l.Name.Contains(q) || l.Phone.Contains(q) || (l.Email != null && l.Email.Contains(q)));

            var total = await query.CountAsync();
            var leads = await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new
                {
                    l.Id, l.Name, l.Phone, l.Email, l.Company,
                    l.Status, l.Priority, l.Source, l.NextFollowUpAt, l.CreatedAt,
                    AssignedTo = l.AssignedTo != null ? l.AssignedTo.FullName : null,
                    Campaign = l.Campaign != null ? l.Campaign.Name : null,
                    CallCount = l.Calls.Count
                })
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, leads });
        });

        group.MapGet("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var lead = await db.Leads
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tc.TenantId);
            if (lead == null) return Results.NotFound();

            // Return a flat DTO — avoids circular reference with navigation properties
            return Results.Ok(new {
                lead.Id, lead.Name, lead.Phone, lead.AlternatePhone,
                lead.Email, lead.Company, lead.Industry,
                lead.City, lead.State, lead.Tags, lead.Notes, lead.Source,
                lead.Status, lead.Priority,
                lead.CampaignId, lead.AssignedToId,
                lead.NextFollowUpAt, lead.LastContactedAt,
                lead.AiScore, lead.AiInsight,
                lead.CreatedAt, lead.UpdatedAt
            });
        });

        group.MapPost("/", async ([FromBody] LeadUpsertDto dto, TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var currentLeadCount = await db.Leads.CountAsync(l => l.TenantId == tc.TenantId);
            if (currentLeadCount >= tc.Tenant!.MaxLeads)
                return Results.BadRequest($"Lead limit reached. Your plan allows up to {tc.Tenant.MaxLeads} leads. Please upgrade.");

            // ?? DNC guard ????????????????????????????????????????????????????
            var normPhone = DncEndpoints.NormalisePhone(dto.Phone);
            var isDnc = await db.DncEntries
                .AnyAsync(d => d.TenantId == tc.TenantId && d.Phone == normPhone);
            if (isDnc)
                return Results.BadRequest(new {
                    error = "DNC",
                    message = $"Cannot create lead for {dto.Phone} — this number is on the Do-Not-Call list."
                });

            // Duplicate phone check
            var existingLead = await db.Leads
                .Where(l => l.TenantId == tc.TenantId && l.Phone == dto.Phone.Trim())
                .Select(l => new { l.Id, l.Name, l.Status })
                .FirstOrDefaultAsync();
            if (existingLead != null)
                return Results.Conflict(new {
                    error = "DUPLICATE",
                    message = $"A lead with phone {dto.Phone} already exists.",
                    existingLeadId = existingLead.Id,
                    existingLeadName = existingLead.Name,
                    existingLeadStatus = existingLead.Status.ToString()
                });

            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var lead = new Lead
            {
                TenantId = tc.TenantId,
                Name = dto.Name,
                Phone = dto.Phone,
                AlternatePhone = dto.AlternatePhone,
                Email = dto.Email,
                Company = dto.Company,
                Industry = dto.Industry,
                City = dto.City,
                State = dto.State,
                Tags = dto.Tags,
                Notes = dto.Notes,
                Source = dto.Source,
                CampaignId = dto.CampaignId,
                AssignedToId = dto.AssignedToId,
                Priority = dto.Priority
            };
            db.Leads.Add(lead);
            db.ActivityLogs.Add(new ActivityLog {
                TenantId = tc.TenantId, LeadId = lead.Id, UserId = userId,
                Type = ActivityType.LeadCreated, Summary = $"Lead created: {lead.Name} ({lead.Phone})"
            });
            await db.SaveChangesAsync();

            // Fire LeadCreated webhook
            var dispatcher = http.RequestServices.GetRequiredService<IWebhookDispatcher>();
            Hangfire.BackgroundJob.Enqueue(() => dispatcher.FireAsync(
                tc.TenantId, WebhookEvent.LeadCreated, new { leadId = lead.Id, lead.Name, lead.Phone }));

            return Results.Created($"/api/leads/{lead.Id}", new {
                lead.Id, lead.Name, lead.Phone, lead.AlternatePhone,
                lead.Email, lead.Company, lead.Industry,
                lead.City, lead.State, lead.Tags, lead.Notes, lead.Source,
                lead.Status, lead.Priority,
                lead.CampaignId, lead.AssignedToId,
                lead.NextFollowUpAt, lead.CreatedAt
            });
        });

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] LeadUpsertDto dto, TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tc.TenantId);
            if (lead == null) return Results.NotFound();

            var oldStatus = lead.Status;
            lead.Name = dto.Name;
            lead.Phone = dto.Phone;
            lead.AlternatePhone = dto.AlternatePhone;
            lead.Email = dto.Email;
            lead.Company = dto.Company;
            lead.Industry = dto.Industry;
            lead.City = dto.City;
            lead.State = dto.State;
            lead.Tags = dto.Tags;
            lead.Notes = dto.Notes;
            lead.Source = dto.Source;
            lead.CampaignId = dto.CampaignId;
            lead.AssignedToId = dto.AssignedToId;
            lead.Priority = dto.Priority;
            lead.Status = dto.Status;
            lead.NextFollowUpAt = dto.NextFollowUpAt;
            lead.UpdatedAt = DateTime.UtcNow;

            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            if (oldStatus != dto.Status)
                db.ActivityLogs.Add(new ActivityLog {
                    TenantId = tc.TenantId, LeadId = id, UserId = userId,
                    Type = ActivityType.StatusChanged,
                    Summary = $"Status changed: {oldStatus} ? {dto.Status}"
                });
            else
                db.ActivityLogs.Add(new ActivityLog {
                    TenantId = tc.TenantId, LeadId = id, UserId = userId,
                    Type = ActivityType.LeadUpdated, Summary = "Lead updated"
                });

            if (oldStatus != LeadStatus.Converted && dto.Status == LeadStatus.Converted)
            {
                var dispatcher = http.RequestServices.GetRequiredService<IWebhookDispatcher>();
                Hangfire.BackgroundJob.Enqueue(() => dispatcher.FireAsync(
                    tc.TenantId, WebhookEvent.LeadConverted, new { leadId = id, lead.Name, lead.Phone }));

                // Push SignalR dashboard update on conversion
                var hub = http.RequestServices.GetRequiredService<IHubContext<CrmHub>>();
                await hub.Clients.Group($"tenant-{tc.TenantId}")
                    .SendAsync("DashboardUpdated", new { reason = "lead_converted" });
            }

            await db.SaveChangesAsync();
            return Results.Ok(new {
                lead.Id, lead.Name, lead.Phone, lead.AlternatePhone,
                lead.Email, lead.Company, lead.Industry,
                lead.City, lead.State, lead.Tags, lead.Notes, lead.Source,
                lead.Status, lead.Priority,
                lead.CampaignId, lead.AssignedToId,
                lead.NextFollowUpAt, lead.UpdatedAt
            });
        });

        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tc.TenantId);
            if (lead == null) return Results.NotFound();
            db.Leads.Remove(lead);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // POST /api/leads/bulk-assign
        group.MapPost("/bulk-assign", async ([FromBody] BulkAssignDto dto, TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var leads = await db.Leads
                .Where(l => l.TenantId == tc.TenantId && dto.LeadIds.Contains(l.Id))
                .ToListAsync();

            foreach (var lead in leads)
            {
                lead.AssignedToId = dto.AssignedToId;
                lead.UpdatedAt = DateTime.UtcNow;
                db.ActivityLogs.Add(new ActivityLog {
                    TenantId = tc.TenantId, LeadId = lead.Id, UserId = userId,
                    Type = ActivityType.LeadAssigned,
                    Summary = $"Bulk assigned to agent {dto.AssignedToId}"
                });
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { updated = leads.Count });
        });

        // POST /api/leads/{id}/score — trigger AI scoring on demand
        group.MapPost("/{id:guid}/score", async (Guid id, TenantContext tc, AppDbContext db, IOpenRouterService ai) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var lead = await db.Leads
                .Include(l => l.Calls.OrderByDescending(c => c.StartedAt).Take(3))
                .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tc.TenantId);
            if (lead == null) return Results.NotFound();
            var tenant = await db.Tenants.FindAsync(tc.TenantId);
            if (tenant == null) return Results.Unauthorized();

            var callSummaries = lead.Calls.Select(c => $"Outcome:{c.Outcome} Sentiment:{c.AiSentiment}").ToList();
            var prompt = $"Rate this lead's conversion probability 0-100. Lead: {lead.Name}, Status: {lead.Status}, " +
                         $"Priority: {lead.Priority}, Calls: {callSummaries.Count}, " +
                         $"Recent calls: {string.Join("; ", callSummaries)}. Reply with ONLY a number 0-100.";
            var scoreStr = await ai.ChatAsync(prompt, null, tenant);
            if (int.TryParse(scoreStr.Trim().Split(' ')[0], out var score))
                lead.AiScore = Math.Clamp(score, 0, 100);

            // Also get an AI insight
            if (lead.Calls.Any(c => !string.IsNullOrEmpty(c.TranscriptText)))
            {
                var transcript = lead.Calls.FirstOrDefault(c => !string.IsNullOrEmpty(c.TranscriptText))?.TranscriptText ?? "";
                var insightPrompt = "In one short sentence, identify if this call transcript mentions a competitor, a strong objection, or a high-interest signal. If none, reply 'none'.";
                var insight = await ai.ChatAsync(insightPrompt, transcript, tenant);
                if (!insight.Equals("none", StringComparison.OrdinalIgnoreCase))
                    lead.AiInsight = insight;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { lead.AiScore, lead.AiInsight });
        });

        // POST /api/leads/import — CSV bulk import
        group.MapPost("/import", async (IFormFile file, TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            if (file == null || file.Length == 0) return Results.BadRequest("No file uploaded.");

            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var currentCount = await db.Leads.CountAsync(l => l.TenantId == tc.TenantId);
            var maxLeads = tc.Tenant!.MaxLeads;

            var imported = 0;
            var skipped = 0;
            var errors = new List<string>();

            using var reader = new System.IO.StreamReader(file.OpenReadStream());
            var header = await reader.ReadLineAsync(); // skip header row
            if (header == null) return Results.BadRequest("Empty file.");

            while (!reader.EndOfStream)
            {
                if (currentCount + imported >= maxLeads)
                {
                    errors.Add($"Lead limit ({maxLeads}) reached. Remaining rows skipped.");
                    break;
                }
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Parse CSV: Name,Phone,Email,Company,City,State,Source,Notes
                var cols = ParseCsvLine(line);
                if (cols.Length < 2 || string.IsNullOrWhiteSpace(cols[0]) || string.IsNullOrWhiteSpace(cols[1]))
                {
                    skipped++;
                    continue;
                }

                // Skip DNC numbers
                var phone = cols[1].Trim();
                var normPhoneImport = DncEndpoints.NormalisePhone(phone);
                var isDncImport = await db.DncEntries.AnyAsync(d => d.TenantId == tc.TenantId && d.Phone == normPhoneImport);
                if (isDncImport) { skipped++; continue; }

                // Skip duplicates (same phone in this tenant)
                if (await db.Leads.AnyAsync(l => l.TenantId == tc.TenantId && l.Phone == phone))
                {
                    skipped++;
                    continue;
                }

                var lead = new Lead
                {
                    TenantId = tc.TenantId,
                    Name = cols[0].Trim(),
                    Phone = phone,
                    Email = cols.Length > 2 ? cols[2].Trim() : null,
                    Company = cols.Length > 3 ? cols[3].Trim() : null,
                    City = cols.Length > 4 ? cols[4].Trim() : null,
                    State = cols.Length > 5 ? cols[5].Trim() : null,
                    Source = cols.Length > 6 ? cols[6].Trim() : "Import",
                    Notes = cols.Length > 7 ? cols[7].Trim() : null,
                };
                db.Leads.Add(lead);
                db.ActivityLogs.Add(new ActivityLog {
                    TenantId = tc.TenantId, LeadId = lead.Id, UserId = userId,
                    Type = ActivityType.LeadCreated, Summary = $"Imported: {lead.Name} ({lead.Phone})"
                });
                imported++;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { imported, skipped, errors });
        }).DisableAntiforgery();

        // POST /api/leads/bulk-status
        group.MapPost("/bulk-status", async ([FromBody] BulkStatusDto dto, TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var leads = await db.Leads
                .Where(l => l.TenantId == tc.TenantId && dto.LeadIds.Contains(l.Id))
                .ToListAsync();
            foreach (var lead in leads)
            {
                var oldStatus = lead.Status;
                lead.Status = dto.Status;
                lead.UpdatedAt = DateTime.UtcNow;
                db.ActivityLogs.Add(new ActivityLog {
                    TenantId = tc.TenantId, LeadId = lead.Id, UserId = userId,
                    Type = ActivityType.StatusChanged,
                    Summary = $"Bulk status change: {oldStatus} ? {dto.Status}"
                });
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { updated = leads.Count });
        });

        // POST /api/leads/bulk-delete
        group.MapPost("/bulk-delete", async ([FromBody] BulkDeleteDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var leads = await db.Leads
                .Where(l => l.TenantId == tc.TenantId && dto.LeadIds.Contains(l.Id))
                .ToListAsync();
            db.Leads.RemoveRange(leads);
            await db.SaveChangesAsync();
            return Results.Ok(new { deleted = leads.Count });
        });

        // POST /api/leads/bulk-reassign-agent - reassign ALL leads from one agent to another
        group.MapPost("/bulk-reassign-agent", async ([FromBody] BulkReassignAgentDto dto, TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var leads = await db.Leads
                .Where(l => l.TenantId == tc.TenantId && l.AssignedToId == dto.FromAgentId)
                .ToListAsync();

            foreach (var lead in leads)
            {
                lead.AssignedToId = dto.ToAgentId;
                lead.UpdatedAt = DateTime.UtcNow;
                db.ActivityLogs.Add(new ActivityLog {
                    TenantId = tc.TenantId, LeadId = lead.Id, UserId = userId,
                    Type = ActivityType.LeadAssigned,
                    Summary = $"Reassigned from agent {dto.FromAgentId} to agent {dto.ToAgentId}"
                });
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { updated = leads.Count });
        });
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var inQuote = false;
        var current = new System.Text.StringBuilder();
        foreach (var ch in line)
        {
            if (ch == '"') { inQuote = !inQuote; }
            else if (ch == ',' && !inQuote) { result.Add(current.ToString()); current.Clear(); }
            else { current.Append(ch); }
        }
        result.Add(current.ToString());
        return result.ToArray();
    }
}

public record LeadUpsertDto(
string Name, string Phone, string? Email, string? Company,
string? AlternatePhone, string? Industry, string? City, string? State, string? Tags,
string? Notes, string? Source, int Priority,
Guid? CampaignId, Guid? AssignedToId,
LeadStatus Status = LeadStatus.New,
DateTime? NextFollowUpAt = null);

public record BulkAssignDto(List<Guid> LeadIds, Guid AssignedToId);
public record BulkReassignAgentDto(Guid FromAgentId, Guid ToAgentId);
public record BulkStatusDto(List<Guid> LeadIds, LeadStatus Status);
public record BulkDeleteDto(List<Guid> LeadIds);
