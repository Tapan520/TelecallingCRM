namespace TelecallingCRM.Data.Models;

public class CustomLeadField
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;       // internal key e.g. "loan_amount"
    public string Label { get; set; } = string.Empty;      // display label e.g. "Loan Amount"
    public string FieldType { get; set; } = "text";        // text, number, date, dropdown
    public string? Options { get; set; }                   // JSON array for dropdown
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
}
