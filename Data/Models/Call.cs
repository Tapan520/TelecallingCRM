namespace TelecallingCRM.Data.Models;

public enum CallOutcome
{
    NoAnswer,
    Busy,
    Callback,
    Interested,
    NotInterested,
    Converted,
    WrongNumber,
    SwitchOff,
    HotLead,
    CallLater,
    Other
}

public enum CallDirection { Outbound, Inbound }

public class Call
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid AgentId { get; set; }

    public CallDirection Direction { get; set; } = CallDirection.Outbound;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public int DurationSeconds { get; set; } = 0;
    public CallOutcome Outcome { get; set; } = CallOutcome.Other;
    public string? Notes { get; set; }
    public string? TranscriptText { get; set; }
    public string? AudioFileUrl { get; set; }
    public string? AiSummary { get; set; }
    public string? AiSentiment { get; set; }      // positive / neutral / negative
    public string? AiInsight { get; set; }         // e.g. "mentioned competitor"
    public bool IsRecorded { get; set; } = false;
    public string? ProviderCallId { get; set; }   // external telephony provider call ID
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Lead Lead { get; set; } = null!;
    public AppUser Agent { get; set; } = null!;
}

