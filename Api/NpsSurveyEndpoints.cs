using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class NpsSurveyEndpoints
{
    public static void MapNpsSurveyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/nps").WithTags("NPS").RequireAuthorization().RequireRateLimiting("api");

        group.MapGet("/", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var surveys = await db.NpsSurveys
                .Where(s => s.TenantId == tc.TenantId)
                .Select(s => new {
                    s.Id, s.Name, s.Status, s.Trigger, s.IsActive, s.CreatedAt,
                    ResponseCount = s.Responses.Count,
                    AvgScore = s.Responses.Any() ? s.Responses.Average(r => (double)r.Score) : 0.0
                })
                .ToListAsync();
            return Results.Ok(surveys);
        });

        group.MapGet("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var s = await db.NpsSurveys.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tc.TenantId);
            return s is null ? Results.NotFound() : Results.Ok(s);
        });

        group.MapPost("/", async ([FromBody] NpsSurveyDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var survey = new NpsSurvey
            {
                TenantId = tc.TenantId,
                Name = dto.Name, IntroText = dto.IntroText,
                Trigger = dto.Trigger, IsActive = dto.IsActive,
                CampaignId = dto.CampaignId, Status = SurveyStatus.Draft
            };
            db.NpsSurveys.Add(survey);
            await db.SaveChangesAsync();
            return Results.Created($"/api/nps/{survey.Id}", new { survey.Id, survey.Name });
        });

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] NpsSurveyDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var survey = await db.NpsSurveys.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tc.TenantId);
            if (survey is null) return Results.NotFound();
            survey.Name = dto.Name; survey.IntroText = dto.IntroText;
            survey.Trigger = dto.Trigger; survey.IsActive = dto.IsActive;
            survey.CampaignId = dto.CampaignId;
            await db.SaveChangesAsync();
            return Results.Ok(new { survey.Id });
        });

        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var survey = await db.NpsSurveys.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tc.TenantId);
            if (survey is null) return Results.NotFound();
            db.NpsSurveys.Remove(survey);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // POST /api/nps/{id}/respond — submit a response (can be unauthenticated for public links)
        group.MapPost("/{id:guid}/respond", async (Guid id, [FromBody] NpsResponseDto dto, TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            if (dto.Score < 0 || dto.Score > 10) return Results.BadRequest("Score must be 0–10.");
            Guid? agentId = null;
            var claim = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (claim != null && Guid.TryParse(claim, out var aid)) agentId = aid;

            var resp = new NpsSurveyResponse
            {
                SurveyId = id, LeadId = dto.LeadId,
                AgentId = agentId, CallId = dto.CallId,
                Score = dto.Score, Feedback = dto.Feedback
            };
            db.NpsSurveyResponses.Add(resp);
            await db.SaveChangesAsync();
            return Results.Ok(new { resp.Id, resp.Score });
        }).AllowAnonymous();

        // GET /api/nps/{id}/responses
        group.MapGet("/{id:guid}/responses", async (Guid id, TenantContext tc, AppDbContext db,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var q = db.NpsSurveyResponses.Where(r => r.SurveyId == id);
            var total = await q.CountAsync();
            var items = await q.OrderByDescending(r => r.RespondedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(r => new {
                    r.Id, r.Score, r.Feedback, r.RespondedAt,
                    LeadName = r.Lead.Name,
                    AgentName = r.Agent != null ? r.Agent.FullName : null
                }).ToListAsync();

            // NPS breakdown
            var allScores = await q.Select(r => r.Score).ToListAsync();
            var promoters = allScores.Count(s => s >= 9);
            var detractors = allScores.Count(s => s <= 6);
            var npsScore = allScores.Count > 0
                ? Math.Round((double)(promoters - detractors) / allScores.Count * 100, 1) : 0.0;

            return Results.Ok(new { total, page, pageSize, npsScore, promoters, detractors, items });
        });
    }
}

public record NpsSurveyDto(string Name, string? IntroText, SurveyTrigger Trigger, bool IsActive, Guid? CampaignId);
public record NpsResponseDto(Guid LeadId, int Score, string? Feedback, Guid? CallId);
