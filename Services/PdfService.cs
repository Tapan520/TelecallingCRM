using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.Json;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Services;

/// <summary>Generates PDF bytes for Invoices and Quotes using QuestPDF.</summary>
public interface IPdfService
{
    byte[] GenerateInvoicePdf(Invoice invoice, string tenantName);
    byte[] GenerateQuotePdf(Quote quote, string tenantName, string leadName);
}

public class PdfService : IPdfService
{
    public PdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // ?? Invoice PDF ??????????????????????????????????????????????????????????
    public byte[] GenerateInvoicePdf(Invoice invoice, string tenantName)
    {
        var lineItems = ParseLineItems(invoice.LineItemsJson);
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.DefaultTextStyle(t => t.FontSize(10));
            page.Header().Element(c => BuildHeader(c, "INVOICE", tenantName,
                invoice.InvoiceNumber,
                invoice.IssuedAt.ToString("dd MMM yyyy"),
                invoice.DueAt?.ToString("dd MMM yyyy") ?? "—",
                invoice.Lead?.Name ?? (invoice.LeadId.ToString()),
                invoice.Status.ToString()));
            page.Content().Element(c => BuildLineItems(c, lineItems,
                invoice.SubTotal, 0m,
                invoice.TaxPercent, invoice.TaxAmount, invoice.Total, invoice.Currency));
            page.Footer().Element(BuildFooter);
        })).GeneratePdf();
    }

    // ?? Quote PDF ????????????????????????????????????????????????????????????
    public byte[] GenerateQuotePdf(Quote quote, string tenantName, string leadName)
    {
        var lineItems = ParseLineItems(quote.LineItemsJson);
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.DefaultTextStyle(t => t.FontSize(10));
            page.Header().Element(c => BuildHeader(c, "QUOTATION", tenantName,
                quote.QuoteNumber,
                quote.CreatedAt.ToString("dd MMM yyyy"),
                quote.ExpiresAt?.ToString("dd MMM yyyy") ?? "—",
                leadName,
                quote.Status.ToString()));
            page.Content().Element(c => BuildLineItems(c, lineItems,
                quote.SubTotal, quote.DiscountAmount,
                quote.TaxPercent, quote.TaxAmount, quote.Total, quote.Currency));
            page.Footer().Element(BuildFooter);
        })).GeneratePdf();
    }

    // ?? Shared Header ?????????????????????????????????????????????????????????
    private static void BuildHeader(IContainer container, string docType, string tenantName,
        string docNumber, string issueDate, string dueOrExpiry, string recipientName, string status)
    {
        container.Column(col =>
        {
            col.Item().Background("#4f46e5").Padding(16).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(tenantName).FontSize(16).Bold().FontColor(Colors.White);
                    c.Item().Text(docType).FontSize(11).FontColor("#c7d2fe");
                });
                row.ConstantItem(130).Column(c =>
                {
                    c.Item().AlignRight().Text(docNumber).Bold().FontColor(Colors.White).FontSize(11);
                    c.Item().AlignRight().Text("Status: " + status).FontColor("#e0e7ff").FontSize(9);
                });
            });

            col.Item().PaddingVertical(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(docType == "INVOICE" ? "Bill To" : "Prepared For").Bold().FontColor("#6b7280").FontSize(9);
                    c.Item().Text(recipientName).Bold().FontSize(11);
                });
                row.ConstantItem(170).Column(c =>
                {
                    TwoColRow(c, "Issue Date:", issueDate);
                    TwoColRow(c, docType == "INVOICE" ? "Due Date:" : "Valid Until:", dueOrExpiry);
                });
            });

            col.Item().LineHorizontal(1).LineColor("#e5e7eb");
            col.Item().Height(8);
        });
    }

    private static void TwoColRow(ColumnDescriptor col, string label, string value)
    {
        col.Item().PaddingVertical(2).Row(r =>
        {
            r.ConstantItem(75).Text(label).FontColor("#6b7280");
            r.RelativeItem().Text(value);
        });
    }

    // ?? Line Items Table ??????????????????????????????????????????????????????
    private static void BuildLineItems(IContainer container, List<PdfLineItem> items,
        decimal subTotal, decimal discount, decimal taxPct, decimal taxAmt, decimal total, string currency)
    {
        container.Column(col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(5);
                    c.RelativeColumn(1);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                });

                table.Header(h =>
                {
                    static void Th(IContainer cell, string text) =>
                        cell.Background("#f3f4f6").Padding(7).Text(text).Bold().FontSize(9);

                    Th(h.Cell(), "Description");
                    Th(h.Cell().AlignCenter(), "Qty");
                    Th(h.Cell().AlignRight(), "Unit Price");
                    Th(h.Cell().AlignRight(), "Amount");
                });

                var even = false;
                foreach (var item in items)
                {
                var bg = even ? "#f9fafb" : "#ffffff";
                    even = !even;
                    table.Cell().Background(bg).Padding(7).Text(item.Description);
                    table.Cell().Background(bg).Padding(7).AlignCenter().Text(item.Qty.ToString());
                    table.Cell().Background(bg).Padding(7).AlignRight().Text(Fmt(item.UnitPrice, currency));
                    table.Cell().Background(bg).Padding(7).AlignRight().Text(Fmt(item.Amount, currency));
                }
            });

            col.Item().Height(14);

            // Totals
            col.Item().AlignRight().Width(220).Column(t =>
            {
                TotalRow(t, "Sub Total", Fmt(subTotal, currency));
                if (discount > 0)
                    TotalRow(t, "Discount", "- " + Fmt(discount, currency));
                TotalRow(t, $"Tax ({taxPct}%)", Fmt(taxAmt, currency));
                t.Item().LineHorizontal(1).LineColor("#4f46e5");
                t.Item().Background("#4f46e5").Padding(7).Row(r =>
                {
                    r.RelativeItem().Text("TOTAL").Bold().FontColor(Colors.White);
                    r.AutoItem().Text(Fmt(total, currency)).Bold().FontColor(Colors.White);
                });
            });
        });
    }

    private static void TotalRow(ColumnDescriptor col, string label, string value)
    {
        col.Item().BorderBottom(1).BorderColor("#e5e7eb").PaddingVertical(5).Row(r =>
        {
            r.RelativeItem().Text(label).FontColor("#6b7280");
            r.AutoItem().Text(value);
        });
    }

    // ?? Footer ????????????????????????????????????????????????????????????????
    private static void BuildFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(1).LineColor("#e5e7eb");
            col.Item().PaddingTop(6).AlignCenter()
                .Text("Generated by TelecallingCRM · Thank you for your business!")
                .FontColor("#9ca3af").FontSize(9);
        });
    }

    // ?? Helpers ???????????????????????????????????????????????????????????????
    private static string Fmt(decimal amount, string currency) => currency switch
    {
        "INR" => "?" + amount.ToString("N2"),
        "USD" => "$" + amount.ToString("N2"),
        "EUR" => "€" + amount.ToString("N2"),
        "GBP" => "£" + amount.ToString("N2"),
        _ => amount.ToString("N2") + " " + currency
    };

    private static List<PdfLineItem> ParseLineItems(string json)
    {
        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<PdfLineItem>>(json, opts) ?? new();
        }
        catch { return new(); }
    }

    private record PdfLineItem(string Description, int Qty, decimal UnitPrice, decimal Amount);
}
