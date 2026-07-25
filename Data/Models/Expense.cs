namespace TelecallingCRM.Data.Models;

public enum ExpenseStatus { Pending, Approved, Rejected }
public enum ExpenseCategory { Travel, Internet, Food, Equipment, Training, Other }

public class Expense
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AgentId { get; set; }

    public ExpenseCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string? ReceiptUrl { get; set; }

    public ExpenseStatus Status { get; set; } = ExpenseStatus.Pending;
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewerNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public AppUser Agent { get; set; } = null!;
    public AppUser? ReviewedBy { get; set; }
}
