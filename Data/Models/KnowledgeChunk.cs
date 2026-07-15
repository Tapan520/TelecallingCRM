namespace TelecallingCRM.Data.Models;

public class KnowledgeChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Category { get; set; }
    /// <summary>
    /// JSON-serialised float[] embedding vector (1536 dims).
    /// Stored as LONGTEXT in MySQL; cosine similarity is computed in memory.
    /// </summary>
    public string? EmbeddingJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
}
