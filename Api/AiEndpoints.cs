using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class AiEndpoints
{
    public static void MapAiEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/ai").WithTags("AI").RequireAuthorization().RequireRateLimiting("api");

        // AI chat with optional RAG from knowledge base
        group.MapPost("/chat", async ([FromBody] AiChatRequest req, TenantContext tc,
            AppDbContext db, IOpenRouterService ai, IKnowledgeService knowledge) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var tenant = await db.Tenants.FindAsync(tc.TenantId);
            if (tenant == null) return Results.Unauthorized();

            // RAG: retrieve relevant knowledge chunks
            var chunks = await knowledge.SearchAsync(tc.TenantId, req.Message, tenant, topK: 4);
            string? systemPrompt = req.SystemPrompt;

            if (chunks.Any())
            {
                var context = string.Join("\n\n", chunks.Select(c => $"[{c.Title}]\n{c.Content}"));
                systemPrompt = (systemPrompt ?? "You are a helpful telecalling CRM assistant.") +
                    $"\n\nRelevant knowledge base context:\n{context}";
            }

            var reply = await ai.ChatAsync(req.Message, systemPrompt, tenant);
            return Results.Ok(new { reply, usedKnowledgeChunks = chunks.Count });
        });

        // Knowledge base management
        group.MapGet("/knowledge", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var chunks = await db.KnowledgeChunks
                .Where(k => k.TenantId == tc.TenantId)
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => new { k.Id, k.Title, k.Category, k.Content, k.CreatedAt })
                .ToListAsync();
            return Results.Ok(chunks);
        });

        group.MapPost("/knowledge", async ([FromBody] KnowledgeChunkDto dto, TenantContext tc,
            AppDbContext db, IKnowledgeService knowledge) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var tenant = await db.Tenants.FindAsync(tc.TenantId);
            if (tenant == null) return Results.Unauthorized();

            await knowledge.AddChunkAsync(tc.TenantId, dto.Title, dto.Content, dto.Category, tenant);
            return Results.Created("/api/ai/knowledge", new { success = true });
        });

        group.MapDelete("/knowledge/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var chunk = await db.KnowledgeChunks
                .FirstOrDefaultAsync(k => k.Id == id && k.TenantId == tc.TenantId);
            if (chunk == null) return Results.NotFound();
            db.KnowledgeChunks.Remove(chunk);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}

public record AiChatRequest(string Message, string? SystemPrompt);
public record KnowledgeChunkDto(string Title, string Content, string? Category);
