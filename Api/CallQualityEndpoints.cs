using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class CallQualityEndpoints
{
    public static void MapCallQualityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/call-quality")
            .WithTags("Call Quality")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        // GET quality scores for a call or agent
        group.MapGet("/", async (TenantContext tc, AppDbContext db, HttpContext http,
            [FromQuery] Guid? agentId, [FromQuery] Guid? callId,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role = http.User.FindFirst(ClaimTypes.Role)?.Value ?? http.User.FindFirst("role")?.Value ?? "";

            var query = db.CallQualityScores.Where(q => q.TenantId == tc.TenantId).AsQueryable();

            if (role != "admin" && role != "manager")
                query = query.Where(q => q.AgentId == callerId);
            else if (agentId.HasValue)
                query = query.Where(q => q.AgentId == agentId.Value);

            if (callId.HasValue) query = query.Where(q => q.CallId == callId.Value);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(q => q.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(q => new {
                    q.Id, q.CallId, q.AgentId,
                    AgentName      = q.Agent.FullName,
                    ReviewedBy     = q.ReviewedBy.FullName,
                    q.Rating, q.Feedback,
                    q.CommunicationScore, q.ProductKnowledgeScore,
                    q.ProblemSolvingScore, q.ProfessionalismScore,
                    AvgScore = (q.CommunicationScore + q.ProductKnowledgeScore + q.ProblemSolvingScore + q.ProfessionalismScore) / 4.0,
                    q.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, items });
        });

        // GET agent average quality score
        group.MapGet("/agent-summary", async (TenantContext tc, AppDbContext db, HttpContext http,
            [FromQuery] Guid? agentId) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var targetId = agentId ?? callerId;

            var scores = await db.CallQualityScores
                .Where(q => q.TenantId == tc.TenantId && q.AgentId == targetId)
                .ToListAsync();

            if (!scores.Any()) return Results.Ok(new { totalReviews = 0, avgRating = 0, avgCommunication = 0, avgProductKnowledge = 0, avgProblemSolving = 0, avgProfessionalism = 0 });

            return Results.Ok(new {
                totalReviews      = scores.Count,
                avgRating         = scores.Average(s => (int)s.Rating),
                avgCommunication  = scores.Average(s => s.CommunicationScore),
                avgProductKnowledge = scores.Average(s => s.ProductKnowledgeScore),
                avgProblemSolving = scores.Average(s => s.ProblemSolvingScore),
                avgProfessionalism = scores.Average(s => s.ProfessionalismScore)
            });
        });

        // POST score a call (admin/manager only)
        group.MapPost("/", async ([FromBody] ScoreCallDto dto, TenantContext tc,
            AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var reviewerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var call = await db.Calls.FirstOrDefaultAsync(c => c.Id == dto.CallId && c.TenantId == tc.TenantId);
            if (call == null) return Results.NotFound("Call not found.");

            var score = new CallQualityScore
            {
                TenantId = tc.TenantId, CallId = dto.CallId,
                AgentId = call.AgentId, ReviewedById = reviewerId,
                Rating = dto.Rating, Feedback = dto.Feedback,
                CommunicationScore = dto.CommunicationScore,
                ProductKnowledgeScore = dto.ProductKnowledgeScore,
                ProblemSolvingScore = dto.ProblemSolvingScore,
                ProfessionalismScore = dto.ProfessionalismScore
            };
            db.CallQualityScores.Add(score);
            await db.SaveChangesAsync();
            return Results.Created($"/api/call-quality/{score.Id}", new { score.Id });
        }).RequireAuthorization(p => p.RequireRole("admin", "manager"));
    }
}

public record ScoreCallDto(Guid CallId, CallQualityRating Rating, string? Feedback,
    int CommunicationScore, int ProductKnowledgeScore, int ProblemSolvingScore, int ProfessionalismScore);
