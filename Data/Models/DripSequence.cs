namespace TelecallingCRM.Data.Models;

public enum AutomationTrigger
{
    LeadCreated,
    LeadStatusChanged,
    CampaignEnrolled,
    FollowUpOverdue
}

public enum AutomationStepType
{
    SendSms,
    SendEmail,
    SendWhatsApp,
    AssignAgent,
    AddTag,
    UpdateStatus,
    Wait
}

public class DripSequence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? CampaignId { get; set; }

    public string Name { get; set; } = string.Empty;
    public AutomationTrigger Trigger { get; set; } = AutomationTrigger.LeadCreated;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public Campaign? Campaign { get; set; }
    public ICollection<DripStep> Steps { get; set; } = new List<DripStep>();
    public ICollection<DripEnrollment> Enrollments { get; set; } = new List<DripEnrollment>();
}

public class DripStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SequenceId { get; set; }

    public int StepOrder { get; set; } = 0;
    public AutomationStepType StepType { get; set; } = AutomationStepType.SendSms;
    public int DelayDays { get; set; } = 0;
    /// <summary>Template text or tag name or status name depending on StepType.</summary>
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DripSequence Sequence { get; set; } = null!;
}

public enum EnrollmentStatus { Active, Completed, Cancelled, Failed }

public class DripEnrollment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SequenceId { get; set; }
    public Guid LeadId { get; set; }
    public Guid TenantId { get; set; }

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
    public int CurrentStep { get; set; } = 0;
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public DateTime? NextRunAt { get; set; }

    public DripSequence Sequence { get; set; } = null!;
    public Lead Lead { get; set; } = null!;
}
