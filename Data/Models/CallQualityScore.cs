namespace TelecallingCRM.Data.Models;

public enum CallQualityRating { Poor = 1, BelowAverage = 2, Average = 3, Good = 4, Excellent = 5 }

public class CallQualityScore
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CallId { get; set; }
    public Guid AgentId { get; set; }
    public Guid ReviewedById { get; set; }

    public CallQualityRating Rating { get; set; }
    public string? Feedback { get; set; }

    // Scoring sub-categories (1-5 each)
    public int CommunicationScore { get; set; }
    public int ProductKnowledgeScore { get; set; }
    public int ProblemSolvingScore { get; set; }
    public int ProfessionalismScore { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public Call Call { get; set; } = null!;
    public AppUser Agent { get; set; } = null!;
    public AppUser ReviewedBy { get; set; } = null!;
}
