using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Services;

public interface IKnowledgeService
{
    Task AddChunkAsync(Guid tenantId, string title, string content, string? category, Tenant tenant, CancellationToken ct = default);
    Task<List<KnowledgeChunk>> SearchAsync(Guid tenantId, string query, Tenant tenant, int topK = 5, CancellationToken ct = default);
}

public class KnowledgeService : IKnowledgeService
{
    private readonly AppDbContext _db;
    private readonly IOpenRouterService _ai;

    public KnowledgeService(AppDbContext db, IOpenRouterService ai)
    {
        _db = db;
        _ai = ai;
    }

    public async Task AddChunkAsync(Guid tenantId, string title, string content, string? category, Tenant tenant, CancellationToken ct = default)
    {
        string? embeddingJson = null;
        try
        {
            var embedding = await _ai.GetEmbeddingAsync(content, tenant, ct);
            embeddingJson = JsonSerializer.Serialize(embedding);
        }
        catch { /* store without embedding; search will skip it */ }

        var chunk = new KnowledgeChunk
        {
            TenantId = tenantId,
            Title = title,
            Content = content,
            Category = category,
            EmbeddingJson = embeddingJson
        };
        _db.KnowledgeChunks.Add(chunk);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<KnowledgeChunk>> SearchAsync(Guid tenantId, string query, Tenant tenant, int topK = 5, CancellationToken ct = default)
    {
        float[] queryEmbedding;
        try { queryEmbedding = await _ai.GetEmbeddingAsync(query, tenant, ct); }
        catch { return new List<KnowledgeChunk>(); }

        // Load all tenant chunks that have embeddings (typically a small set per tenant)
        var chunks = await _db.KnowledgeChunks
            .Where(k => k.TenantId == tenantId && k.EmbeddingJson != null)
            .ToListAsync(ct);

        // Rank by cosine similarity in memory
        return chunks
            .Select(k =>
            {
                float[]? vec = null;
                try { vec = JsonSerializer.Deserialize<float[]>(k.EmbeddingJson!); } catch { }
                return (chunk: k, score: vec != null ? CosineSimilarity(queryEmbedding, vec) : -1f);
            })
            .Where(x => x.score >= 0)
            .OrderByDescending(x => x.score)
            .Take(topK)
            .Select(x => x.chunk)
            .ToList();
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0f;
        float dot = 0f, normA = 0f, normB = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        float denom = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denom == 0f ? 0f : dot / denom;
    }
}
