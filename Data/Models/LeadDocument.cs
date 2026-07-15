namespace TelecallingCRM.Data.Models;

public enum DocumentType { PAN, Aadhar, Contract, Quotation, Invoice, VoiceFile, Other }

public class LeadDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid UploadedById { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public DocumentType Type { get; set; } = DocumentType.Other;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public Lead Lead { get; set; } = null!;
    public AppUser UploadedBy { get; set; } = null!;
}
