namespace TelecallingCRM.Data.Models;

public enum CallControlAction { Mute, Unmute, Hold, Resume, Transfer, Conference }

/// <summary>
/// Records each in-call control action (mute, hold, transfer, conference)
/// taken during a live call. Provides an audit trail and enables
/// front-end state reconstruction on page reload.
/// </summary>
public class CallControlEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CallId { get; set; }
    public Guid AgentId { get; set; }
    public CallControlAction Action { get; set; }
    /// <summary>For Transfer/Conference: the target agent or phone number.</summary>
    public string? TargetParty { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public Call Call { get; set; } = null!;
    public AppUser Agent { get; set; } = null!;
}
