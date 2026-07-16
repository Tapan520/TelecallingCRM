using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Services;

public interface IInvoiceService
{
    Task<Invoice> CreateInvoiceAsync(Guid tenantId, Guid leadId, Guid createdById,
        List<InvoiceLineItem> lineItems, decimal taxPercent, string? description,
        string? notes, DateTime? dueAt, Guid? paymentId, CancellationToken ct = default);

    Task<Invoice?> GetInvoiceAsync(Guid invoiceId, Guid tenantId, CancellationToken ct = default);
    Task MarkPaidAsync(Guid invoiceId, Guid tenantId, CancellationToken ct = default);
    Task VoidInvoiceAsync(Guid invoiceId, Guid tenantId, CancellationToken ct = default);
}

public record InvoiceLineItem(string Description, int Qty, decimal UnitPrice)
{
    public decimal Amount => Qty * UnitPrice;
}

public class InvoiceService : IInvoiceService
{
    private readonly AppDbContext _db;
    private readonly ILogger<InvoiceService> _logger;

    public InvoiceService(AppDbContext db, ILogger<InvoiceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Invoice> CreateInvoiceAsync(
        Guid tenantId, Guid leadId, Guid createdById,
        List<InvoiceLineItem> lineItems, decimal taxPercent, string? description,
        string? notes, DateTime? dueAt, Guid? paymentId, CancellationToken ct = default)
    {
        var number = await GenerateInvoiceNumberAsync(tenantId, ct);
        var subTotal = lineItems.Sum(l => l.Amount);
        var taxAmount = Math.Round(subTotal * taxPercent / 100m, 2);
        var total = subTotal + taxAmount;

        var invoice = new Invoice
        {
            TenantId      = tenantId,
            LeadId        = leadId,
            CreatedById   = createdById,
            PaymentId     = paymentId,
            InvoiceNumber = number,
            SubTotal      = subTotal,
            TaxPercent    = taxPercent,
            TaxAmount     = taxAmount,
            Total         = total,
            Description   = description,
            Notes         = notes,
            DueAt         = dueAt,
            LineItemsJson = JsonSerializer.Serialize(lineItems),
            Status        = InvoiceStatus.Draft
        };
        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Invoice {Number} created for lead {LeadId}", number, leadId);
        return invoice;
    }

    public async Task<Invoice?> GetInvoiceAsync(Guid invoiceId, Guid tenantId, CancellationToken ct = default)
        => await _db.Invoices
            .Include(i => i.Lead)
            .Include(i => i.CreatedBy)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == tenantId, ct);

    public async Task MarkPaidAsync(Guid invoiceId, Guid tenantId, CancellationToken ct = default)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == tenantId, ct);
        if (invoice == null) return;
        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAt = DateTime.UtcNow;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task VoidInvoiceAsync(Guid invoiceId, Guid tenantId, CancellationToken ct = default)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == tenantId, ct);
        if (invoice == null) return;
        invoice.Status = InvoiceStatus.Void;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<string> GenerateInvoiceNumberAsync(Guid tenantId, CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _db.Invoices.CountAsync(i => i.TenantId == tenantId, ct);
        return $"INV-{year}-{(count + 1):D4}";
    }
}
