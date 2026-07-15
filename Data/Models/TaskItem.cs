namespace TelecallingCRM.Data.Models;

public enum TaskPriority { Low, Normal, High, Urgent }
public enum TaskStatus { Pending, InProgress, Completed, Overdue, Cancelled }

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid AssignedToId { get; set; }
    public Guid CreatedById { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public DateTime DueAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public Lead? Lead { get; set; }
    public AppUser AssignedTo { get; set; } = null!;
    public AppUser CreatedBy { get; set; } = null!;
    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
}

public class TaskComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TaskItem Task { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}
