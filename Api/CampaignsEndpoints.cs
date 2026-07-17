using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class CampaignsEndpoints
{
    public static void MapCampaignsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/campaigns").WithTags("Campaigns").RequireAuthorization().RequireRateLimiting("api");

        group.MapGet("/", async (TenantContext tc, AppDbContext db,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? q = null, [FromQuery] int? type = null) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var query = db.Campaigns
                .Where(c => c.TenantId == tc.TenantId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(c => c.Name.Contains(q) || (c.Description != null && c.Description.Contains(q)));
            if (type.HasValue)
                query = query.Where(c => (int)c.Type == type.Value);

            var total = await query.CountAsync();
            var campaigns = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(c => new
                {
                    c.Id, c.Name, c.Description, c.Status, c.Type,
                    c.StartDate, c.EndDate,
                    c.TargetCallsPerDay, c.Script, c.CreatedAt,
                    LeadCount = c.Leads.Count
                }).ToListAsync();
            return Results.Ok(new { total, page, pageSize, campaigns });
        });

        group.MapPost("/", async ([FromBody] CampaignUpsertDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var campaign = new Campaign
            {
                TenantId = tc.TenantId,
                Name = dto.Name,
                Description = dto.Description,
                Type = dto.Type,
                Script = dto.Script,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                TargetCallsPerDay = dto.TargetCallsPerDay
            };
            db.Campaigns.Add(campaign);
            await db.SaveChangesAsync();
            return Results.Created($"/api/campaigns/{campaign.Id}", campaign);
        });

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] CampaignUpsertDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tc.TenantId);
            if (campaign == null) return Results.NotFound();
            campaign.Name = dto.Name;
            campaign.Description = dto.Description;
            campaign.Type = dto.Type;
            campaign.Script = dto.Script;
            campaign.StartDate = dto.StartDate;
            campaign.EndDate = dto.EndDate;
            campaign.TargetCallsPerDay = dto.TargetCallsPerDay;
            campaign.Status = dto.Status;
            await db.SaveChangesAsync();
            return Results.Ok(campaign);
        });

        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tc.TenantId);
            if (campaign == null) return Results.NotFound();
            db.Campaigns.Remove(campaign);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // GET /api/campaigns/{id}/stats — per-campaign analytics
        group.MapGet("/{id:guid}/stats", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tc.TenantId);
            if (campaign == null) return Results.NotFound();

            var leads = await db.Leads.Where(l => l.CampaignId == id).ToListAsync();
            var leadIds = leads.Select(l => l.Id).ToList();

            var callStats = await db.Calls
                .Where(c => leadIds.Contains(c.LeadId))
                .GroupBy(c => c.AgentId)
                .Select(g => new {
                    AgentId = g.Key,
                    Calls = g.Count(),
                    Converted = g.Count(c => c.Outcome == CallOutcome.Converted),
                    TalkSeconds = g.Sum(c => c.DurationSeconds)
                })
                .Join(db.Users, a => a.AgentId, u => u.Id, (a, u) => new {
                    u.FullName, a.Calls, a.Converted, a.TalkSeconds
                })
                .ToListAsync();

            return Results.Ok(new {
                campaign.Id, campaign.Name, campaign.Status,
                TotalLeads = leads.Count,
                Converted = leads.Count(l => l.Status == LeadStatus.Converted),
                Interested = leads.Count(l => l.Status == LeadStatus.Interested),
                NotInterested = leads.Count(l => l.Status == LeadStatus.NotInterested),
                TotalCalls = callStats.Sum(a => a.Calls),
                agentBreakdown = callStats
            });
        });

        // POST /api/campaigns/{id}/leads — add leads to campaign
        group.MapPost("/{id:guid}/leads", async (Guid id, [FromBody] CampaignLeadsDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tc.TenantId);
            if (campaign == null) return Results.NotFound();

            var leads = await db.Leads
                .Where(l => l.TenantId == tc.TenantId && dto.LeadIds.Contains(l.Id))
                .ToListAsync();

            foreach (var lead in leads)
                lead.CampaignId = id;

            await db.SaveChangesAsync();
            return Results.Ok(new { added = leads.Count });
        });

        // DELETE /api/campaigns/{id}/leads/{leadId} — remove lead from campaign
        group.MapDelete("/{id:guid}/leads/{leadId:guid}", async (Guid id, Guid leadId, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == leadId && l.CampaignId == id && l.TenantId == tc.TenantId);
            if (lead == null) return Results.NotFound();
            lead.CampaignId = null;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // POST /api/campaigns/{id}/broadcast — send SMS/WhatsApp/Email to all campaign leads
        group.MapPost("/{id:guid}/broadcast", async (Guid id, [FromBody] CampaignBroadcastDto dto,
            TenantContext tc, AppDbContext db, HttpContext http, IMessageDispatcher dispatcher) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tc.TenantId);
            if (campaign == null) return Results.NotFound();

            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var leads = await db.Leads
                .Where(l => l.CampaignId == id && l.TenantId == tc.TenantId
                         && l.Status != LeadStatus.Converted && l.Status != LeadStatus.Dead)
                .ToListAsync();

            var queued = 0;
            foreach (var lead in leads)
            {
                var body = dto.Message.Replace("{{name}}", lead.Name)
                                      .Replace("{{phone}}", lead.Phone)
                                      .Replace("{{company}}", lead.Company ?? "");

                if (dto.Channel == "sms")
                {
                    var msg = new SmsMessage {
                        TenantId = tc.TenantId, LeadId = lead.Id, SentById = userId,
                        ToPhone = lead.Phone, Body = body,
                        Type = SmsMessageType.Bulk, Status = SmsMessageStatus.Queued
                    };
                    db.SmsMessages.Add(msg);
                    Hangfire.BackgroundJob.Enqueue<IMessageDispatcher>(d => d.SendSmsAsync(tc.TenantId, lead.Phone, body));
                }
                else if (dto.Channel == "whatsapp")
                {
                    var msg = new WhatsAppMessage {
                        TenantId = tc.TenantId, LeadId = lead.Id, SentById = userId,
                        ToPhone = lead.Phone, Body = body, Status = WhatsAppMessageStatus.Queued
                    };
                    db.WhatsAppMessages.Add(msg);
                    Hangfire.BackgroundJob.Enqueue<IMessageDispatcher>(d => d.SendWhatsAppAsync(tc.TenantId, lead.Phone, body, null));
                }
                else if (dto.Channel == "email" && !string.IsNullOrWhiteSpace(lead.Email))
                {
                    var msg = new EmailMessage {
                        TenantId = tc.TenantId, LeadId = lead.Id, SentById = userId,
                        ToEmail = lead.Email, Subject = dto.Subject ?? campaign.Name,
                        Body = body, IsHtml = false, Status = EmailStatus.Queued
                    };
                    db.EmailMessages.Add(msg);
                    Hangfire.BackgroundJob.Enqueue<IMessageDispatcher>(d => d.SendEmailAsync(tc.TenantId, lead.Email!, dto.Subject ?? campaign.Name, body));
                }
                queued++;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { queued, channel = dto.Channel });
        });

        // GET /api/campaigns/{id}/analytics
        group.MapGet("/{id:guid}/analytics", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tc.TenantId);
            if (campaign == null) return Results.NotFound();

            var leads = await db.Leads
                .Where(l => l.TenantId == tc.TenantId && l.CampaignId == id)
                .Select(l => new { l.Id, l.Status })
                .ToListAsync();

            var calls = await db.Calls
                .Where(c => c.TenantId == tc.TenantId && leads.Select(l => l.Id).Contains(c.LeadId))
                .Select(c => new { c.Outcome, c.DurationSeconds, c.StartedAt, c.AgentId,
                    AgentName = c.Agent.FullName })
                .ToListAsync();

            // KPIs
            var totalLeads    = leads.Count;
            var totalCalls    = calls.Count;
            var converted     = leads.Count(l => l.Status == LeadStatus.Converted);
            var avgTalk       = calls.Count > 0 ? (int)calls.Average(c => c.DurationSeconds) : 0;

            // Outcome breakdown (index = enum int value)
            var outcomeBreakdown = Enumerable.Range(0, 11)
                .Select(i => calls.Count(c => (int)c.Outcome == i))
                .ToArray();

            // Lead status breakdown
            var leadsByStatus = leads
                .GroupBy(l => l.Status.ToString())
                .Select(g => new { status = g.Key, count = g.Count() })
                .OrderBy(x => x.status)
                .ToList();

            // Daily calls (last 30 days)
            var since = DateTime.UtcNow.Date.AddDays(-29);
            var dailyCalls = calls
                .Where(c => c.StartedAt >= since)
                .GroupBy(c => c.StartedAt.Date.ToString("dd MMM"))
                .Select(g => new {
                    date = g.Key,
                    total = g.Count(),
                    converted = g.Count(c => c.Outcome == CallOutcome.Converted)
                })
                .OrderBy(x => x.date)
                .ToList();

            // Agent leaderboard
            var agentBreakdown = calls
                .GroupBy(c => new { c.AgentId, c.AgentName })
                .Select(g => new {
                    agentName = g.Key.AgentName,
                    calls     = g.Count(),
                    converted = g.Count(c => c.Outcome == CallOutcome.Converted),
                    avgTalkSeconds = g.Count() > 0 ? (int)g.Average(c => c.DurationSeconds) : 0
                })
                .OrderByDescending(x => x.converted)
                .ToList();

            return Results.Ok(new {
                id = campaign.Id,
                name = campaign.Name,
                status = campaign.Status.ToString(),
                totalLeads, totalCalls, convertedLeads = converted,
                avgTalkSeconds = avgTalk,
                outcomeBreakdown, leadsByStatus, dailyCalls, agentBreakdown
            });
        });
    }
}

public record CampaignUpsertDto(
string Name, string? Description, string? Script,
DateTime? StartDate, DateTime? EndDate, int TargetCallsPerDay,
CampaignStatus Status = CampaignStatus.Draft,
CampaignType Type = CampaignType.ColdCalling);

public record CampaignLeadsDto(List<Guid> LeadIds);
public record CampaignBroadcastDto(string Channel, string Message, string? Subject);
