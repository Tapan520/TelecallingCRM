using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/payments")
            .WithTags("Payments")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        // GET — list payments for tenant (filterable by leadId)
        group.MapGet("/", async (TenantContext tc, AppDbContext db,
            [FromQuery] Guid? leadId, [FromQuery] string? status,
            [FromQuery] string? from, [FromQuery] string? to,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var query = db.Payments
                .Where(p => p.TenantId == tc.TenantId)
                .AsQueryable();

            if (leadId.HasValue) query = query.Where(p => p.LeadId == leadId);
            if (Enum.TryParse<PaymentStatus>(status, true, out var ps))
                query = query.Where(p => p.Status == ps);
            if (DateTime.TryParse(from, out var fromDate))
                query = query.Where(p => p.CreatedAt >= fromDate.ToUniversalTime());
            if (DateTime.TryParse(to, out var toDate))
                query = query.Where(p => p.CreatedAt <= toDate.AddDays(1).ToUniversalTime());

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(p => new {
                    p.Id, p.Amount, p.Currency, p.Status, p.Description,
                    p.ReceiptNumber, p.RazorpayOrderId, p.RazorpayPaymentId,
                    p.CreatedAt, p.CapturedAt,
                    LeadName = p.Lead.Name, p.LeadId,
                    RecordedBy = p.RecordedBy.FullName
                })
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, items });
        });

        // GET — payment summary for a lead
        group.MapGet("/lead/{leadId:guid}/summary", async (Guid leadId,
            TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var payments = await db.Payments
                .Where(p => p.TenantId == tc.TenantId && p.LeadId == leadId)
                .ToListAsync();

            return Results.Ok(new {
                total = payments.Count,
                totalAmount = payments.Where(p => p.Status == PaymentStatus.Captured).Sum(p => p.Amount),
                pending = payments.Count(p => p.Status == PaymentStatus.Pending),
                captured = payments.Count(p => p.Status == PaymentStatus.Captured),
                refunded = payments.Count(p => p.Status == PaymentStatus.Refunded)
            });
        });

        // POST — create a Razorpay order and record a pending payment
        group.MapPost("/create-order", async ([FromBody] CreatePaymentOrderDto dto,
            TenantContext tc, AppDbContext db, IHttpClientFactory httpFactory, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            // Load Razorpay config for tenant
            var cfg = await db.IntegrationConfigs
                .FirstOrDefaultAsync(i => i.TenantId == tc.TenantId && i.Provider == "razorpay");
            if (cfg == null || !cfg.IsEnabled)
                return Results.BadRequest("Razorpay integration is not configured for this tenant.");

            var config = System.Text.Json.JsonSerializer
                .Deserialize<Dictionary<string, string>>(cfg.ConfigJson)!;

            config.TryGetValue("KeyId", out var keyId);
            config.TryGetValue("KeySecret", out var keySecret);

            if (string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(keySecret))
                return Results.BadRequest("Razorpay KeyId and KeySecret are required.");

            var userId = Guid.Parse(
                http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            // Create order via Razorpay REST API
            var client = httpFactory.CreateClient();
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
            client.DefaultRequestHeaders.Add("Authorization", $"Basic {credentials}");

            var orderPayload = new
            {
                amount = (long)(dto.Amount * 100), // Razorpay uses paise
                currency = dto.Currency ?? "INR",
                receipt = $"rcpt_{Guid.NewGuid():N}",
                notes = new { leadId = dto.LeadId.ToString(), description = dto.Description }
            };

            var response = await client.PostAsJsonAsync(
                "https://api.razorpay.com/v1/orders", orderPayload);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                return Results.Problem($"Razorpay error: {err}", statusCode: 502);
            }

            var rzpOrder = await response.Content
                .ReadFromJsonAsync<Dictionary<string, object>>();
            var orderId = rzpOrder?["id"]?.ToString() ?? string.Empty;

            // Persist the pending payment record
            var payment = new Payment
            {
                TenantId = tc.TenantId,
                LeadId = dto.LeadId,
                RecordedById = userId,
                RazorpayOrderId = orderId,
                Amount = dto.Amount,
                Currency = dto.Currency ?? "INR",
                Description = dto.Description,
                ReceiptNumber = orderPayload.receipt,
                Status = PaymentStatus.Pending
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync();

            return Results.Ok(new {
                payment.Id,
                orderId,
                amount = orderPayload.amount,
                currency = orderPayload.currency,
                keyId  // returned so the frontend can open Razorpay Checkout
            });
        });

        // POST — verify Razorpay payment signature and capture
        group.MapPost("/verify", async ([FromBody] VerifyPaymentDto dto,
            TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var payment = await db.Payments
                .FirstOrDefaultAsync(p => p.RazorpayOrderId == dto.RazorpayOrderId
                                       && p.TenantId == tc.TenantId);
            if (payment == null) return Results.NotFound("Payment record not found.");

            // Load Razorpay webhook secret for signature verification
            var cfg = await db.IntegrationConfigs
                .FirstOrDefaultAsync(i => i.TenantId == tc.TenantId && i.Provider == "razorpay");
            var config = cfg != null
                ? System.Text.Json.JsonSerializer
                    .Deserialize<Dictionary<string, string>>(cfg.ConfigJson)
                : null;
            string? resolvedSecret = null;
            config?.TryGetValue("KeySecret", out resolvedSecret);

            // Verify HMAC-SHA256 signature
            if (!string.IsNullOrEmpty(resolvedSecret))
            {
                var message = $"{dto.RazorpayOrderId}|{dto.RazorpayPaymentId}";
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(resolvedSecret!));
                var expected = Convert.ToHexString(
                    hmac.ComputeHash(Encoding.UTF8.GetBytes(message))).ToLower();

                if (!string.Equals(expected, dto.RazorpaySignature, StringComparison.OrdinalIgnoreCase))
                    return Results.BadRequest("Payment signature verification failed.");
            }

            var userId = Guid.Parse(
                http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            payment.RazorpayPaymentId = dto.RazorpayPaymentId;
            payment.RazorpaySignature = dto.RazorpaySignature;
            payment.Status = PaymentStatus.Captured;
            payment.CapturedAt = DateTime.UtcNow;

            // Mark lead as Converted on payment capture
            var lead = await db.Leads.FindAsync(payment.LeadId);
            if (lead != null && lead.Status != LeadStatus.Converted)
                lead.Status = LeadStatus.Converted;

            db.ActivityLogs.Add(new ActivityLog
            {
                TenantId = tc.TenantId, LeadId = payment.LeadId, UserId = userId,
                Type = ActivityType.PaymentReceived,
                Summary = $"Payment of {payment.Currency} {payment.Amount:N2} captured (Razorpay: {dto.RazorpayPaymentId})"
            });

            // Fire webhook
            Hangfire.BackgroundJob.Enqueue<IWebhookDispatcher>(d =>
                d.FireAsync(tc.TenantId, WebhookEvent.LeadConverted,
                    new { payment.LeadId, payment.Amount, payment.RazorpayPaymentId }));

            await db.SaveChangesAsync();
            return Results.Ok(new { payment.Id, payment.Status, payment.CapturedAt });
        });

        // POST — record a manual / offline payment (cash, bank transfer, etc.)
        group.MapPost("/manual", async ([FromBody] ManualPaymentDto dto,
            TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(
                http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var payment = new Payment
            {
                TenantId = tc.TenantId,
                LeadId = dto.LeadId,
                RecordedById = userId,
                Amount = dto.Amount,
                Currency = dto.Currency ?? "INR",
                Description = dto.Description,
                ReceiptNumber = dto.ReceiptNumber,
                Status = PaymentStatus.Captured,
                CapturedAt = DateTime.UtcNow
            };
            db.Payments.Add(payment);

            db.ActivityLogs.Add(new ActivityLog
            {
                TenantId = tc.TenantId, LeadId = dto.LeadId, UserId = userId,
                Type = ActivityType.PaymentReceived,
                Summary = $"Manual payment of {dto.Currency ?? "INR"} {dto.Amount:N2} recorded"
            });

            await db.SaveChangesAsync();
            return Results.Created($"/api/payments/{payment.Id}", new { payment.Id, payment.Status });
        });

        // POST — refund a captured payment
        group.MapPost("/{id:guid}/refund", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var payment = await db.Payments
                .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tc.TenantId);
            if (payment == null) return Results.NotFound();
            if (payment.Status != PaymentStatus.Captured)
                return Results.BadRequest("Only captured payments can be refunded.");

            payment.Status = PaymentStatus.Refunded;
            await db.SaveChangesAsync();
            return Results.Ok(new { payment.Status });
        });
    }
}

public record CreatePaymentOrderDto(
    Guid LeadId, decimal Amount, string? Currency, string? Description);

public record VerifyPaymentDto(
    string RazorpayOrderId, string RazorpayPaymentId, string RazorpaySignature);

public record ManualPaymentDto(
    Guid LeadId, decimal Amount, string? Currency, string? Description, string? ReceiptNumber);
