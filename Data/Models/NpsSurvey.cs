namespace TelecallingCRM.Data.Models;

public enum SurveyStatus { Active, Closed, Draft }
public enum SurveyTrigger { AfterCall, AfterConversion, Manual, Scheduled }

public class NpsSurvey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? CampaignId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? IntroText { get; set; }
    public SurveyStatus Status { get; set; } = SurveyStatus.Draft;
    public SurveyTrigger Trigger { get; set; } = SurveyTrigger.Manual;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public Campaign? Campaign { get; set; }
    public ICollection<NpsSurveyResponse> Responses { get; set; } = new List<NpsSurveyResponse>();
}

public class NpsSurveyResponse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SurveyId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid? AgentId { get; set; }
    public Guid? CallId { get; set; }

    /// <summary>NPS score 0-10.</summary>
    public int Score { get; set; }
    public string? Feedback { get; set; }
    public DateTime RespondedAt { get; set; } = DateTime.UtcNow;

    public NpsSurvey Survey { get; set; } = null!;
    public Lead Lead { get; set; } = null!;
    public AppUser? Agent { get; set; }
    public Call? Call { get; set; }
}
