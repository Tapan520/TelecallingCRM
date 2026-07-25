namespace TelecallingCRM.Data.Models;

public enum OnboardingStepStatus { Pending, Completed, Skipped }

public class OnboardingChecklist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AgentId { get; set; }

    public string StepName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int StepOrder { get; set; }
    public OnboardingStepStatus Status { get; set; } = OnboardingStepStatus.Pending;
    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public AppUser Agent { get; set; } = null!;
}
