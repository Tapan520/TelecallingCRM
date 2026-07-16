using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

/// <summary>
/// Receives and processes Razorpay server-to-server webhook events:
///   payment.captured  ? mark Payment Captured, activity log
///   payment.failed    ? mark Payment Failed, notify agent
///   refund.created    ? mark Payment Refunded, activity log
///
/// Endpoint: POST /api/webhooks/razorpay
/// This is unauthenticated but verified via HMAC-SHA256 of the raw body
/// against the tenant's configured Razorpay WebhookSecret.
/// </summary>
public static class RazorpayWebhookEndpoints
{
    public static void MapRazorpayWebhookEndpoints(this WebApplication app)
    {
        // AllowAnonymous – Razorpay calls this server-to-server; auth is via HMAC signature
        app.MapPost("/api/webhooks/razorpay", async (HttpContext http, AppDbContext db) =>
        {
            // ?? 1. Buffer & read raw body (needed for signature check) ????????
            http.Request.EnableBuffering();
            using var ms = new MemoryStream();
            await http.Request.Body.CopyToAsync(ms);
            var rawBody = Encoding.UTF8.GetString(ms.ToArray());
            http.Request.Body.Position = 0;

            // ?? 2. Parse the JSON payload ?????????????????????????????????????
            JsonDocument doc;
            try { doc = JsonDocument.Parse(rawBody); }
            catch { return Results.BadRequest("Invalid JSON body."); }

            using (doc)
            {
                var root = doc.RootElement;

                // event name e.g. "payment.captured"
                if (!root.TryGetProperty("event", out var eventProp))
                    return Results.BadRequest("Missing 'event' field.");
                var eventName = eventProp.GetString() ?? string.Empty;

                // ?? 3. Resolve which tenant owns this payment ?????????????????
                // Razorpay sends orderId inside entity ? we use it to look up the tenant
                var orderId = TryGetOrderId(root, eventName);
                if (string.IsNullOrEmpty(orderId))
                    return Results.Ok(new { message = "Event ignored – no order_id found." });

                var payment = await db.Payments
                    .FirstOrDefaultAsync(p => p.RazorpayOrderId == orderId);
                if (payment == null)
                    return Results.Ok(new { message = "Payment record not found; event ignored." });

                // ?? 4. Verify HMAC-SHA256 signature ???????????????????????????
                var cfg = await db.IntegrationConfigs
                    .FirstOrDefaultAsync(i => i.TenantId == payment.TenantId
                                           && i.Provider == "razorpay");

                if (cfg != null && cfg.IsEnabled)
                {
                    var configMap = JsonSerializer
                        .Deserialize<Dictionary<string, string>>(cfg.ConfigJson) ?? new();
                    configMap.TryGetValue("WebhookSecret", out var webhookSecret);

                    if (!string.IsNullOrEmpty(webhookSecret))
                    {
                        var signature = http.Request.Headers["X-Razorpay-Signature"]
                                                    .FirstOrDefault() ?? string.Empty;
                        if (!VerifySignature(rawBody, signature, webhookSecret))
                        {
                            return Results.Json(new { error = "Signature mismatch." },
                                statusCode: StatusCodes.Status400BadRequest);
                        }
                    }
                }

                // ?? 5. Handle the event ???????????????????????????????????????
                switch (eventName)
                {
                    case "payment.captured":
                    {
                        if (payment.Status == PaymentStatus.Captured)
                            return Results.Ok(new { message = "Already captured." });

                        var paymentId = TryGetPaymentId(root);
                        payment.Status            = PaymentStatus.Captured;
                        payment.CapturedAt        = DateTime.UtcNow;
                        payment.RazorpayPaymentId = paymentId ?? payment.RazorpayPaymentId;

                        db.ActivityLogs.Add(new ActivityLog
                        {
                            TenantId = payment.TenantId,
                            LeadId   = payment.LeadId,
                            UserId   = payment.RecordedById,
                            Type     = ActivityType.PaymentReceived,
                            Summary  = $"Razorpay webhook: payment.captured – " +
                                       $"{payment.Currency} {payment.Amount:N2} " +
                                       $"(pid: {payment.RazorpayPaymentId})"
                        });

                        // Mark lead as Converted on capture
                        var lead = await db.Leads.FindAsync(payment.LeadId);
                        if (lead != null && lead.Status != LeadStatus.Converted)
                            lead.Status = LeadStatus.Converted;

                        // Fire outgoing webhook
                        Hangfire.BackgroundJob.Enqueue<IWebhookDispatcher>(d =>
                            d.FireAsync(payment.TenantId, WebhookEvent.LeadConverted,
                                new { payment.LeadId, payment.Amount,
                                      payment.RazorpayPaymentId }));
                        break;
                    }

                    case "payment.failed":
                    {
                        payment.Status = PaymentStatus.Failed;

                        // Notify the agent who recorded the payment
                        db.Notifications.Add(new Notification
                        {
                            TenantId = payment.TenantId,
                            UserId   = payment.RecordedById,
                            Type     = NotificationType.SystemAlert,
                            Title    = "Payment Failed",
                            Body     = $"Razorpay payment for order {payment.RazorpayOrderId} " +
                                       $"({payment.Currency} {payment.Amount:N2}) failed.",
                            Link     = $"/Leads/Timeline/{payment.LeadId}"
                        });

                        db.ActivityLogs.Add(new ActivityLog
                        {
                            TenantId = payment.TenantId,
                            LeadId   = payment.LeadId,
                            UserId   = payment.RecordedById,
                            Type     = ActivityType.PaymentReceived,
                            Summary  = $"Razorpay webhook: payment.failed – " +
                                       $"order {payment.RazorpayOrderId}"
                        });
                        break;
                    }

                    case "refund.created":
                    {
                        payment.Status = PaymentStatus.Refunded;

                        db.ActivityLogs.Add(new ActivityLog
                        {
                            TenantId = payment.TenantId,
                            LeadId   = payment.LeadId,
                            UserId   = payment.RecordedById,
                            Type     = ActivityType.PaymentReceived,
                            Summary  = $"Razorpay webhook: refund.created – " +
                                       $"order {payment.RazorpayOrderId}"
                        });
                        break;
                    }

                    default:
                        // Unknown event – acknowledge and ignore
                        return Results.Ok(new { message = $"Event '{eventName}' not handled." });
                }

                await db.SaveChangesAsync();
                return Results.Ok(new { message = $"Event '{eventName}' processed." });
            }
        })
        .AllowAnonymous()
        .WithTags("Payments")
        .ExcludeFromDescription(); // hide from Swagger to avoid confusion with the admin endpoints
    }

    // ?? Helpers ???????????????????????????????????????????????????????????????

    private static bool VerifySignature(string body, string signature, string secret)
    {
        if (string.IsNullOrEmpty(signature)) return false;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLower();
        return string.Equals(expected, signature, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Extracts razorpay_order_id from the payload depending on event type.</summary>
    private static string? TryGetOrderId(JsonElement root, string eventName)
    {
        try
        {
            // Razorpay structure: { "payload": { "payment": { "entity": { "order_id": "..." } } } }
            // For refunds:       { "payload": { "refund":  { "entity": { "acquirer_data": ... } } } }
            if (root.TryGetProperty("payload", out var payload))
            {
                var entityKey = eventName.StartsWith("refund") ? "refund" : "payment";
                if (payload.TryGetProperty(entityKey, out var entityWrapper) &&
                    entityWrapper.TryGetProperty("entity", out var entity))
                {
                    if (entity.TryGetProperty("order_id", out var oid))
                        return oid.GetString();
                }
            }
        }
        catch { /* ignore */ }
        return null;
    }

    private static string? TryGetPaymentId(JsonElement root)
    {
        try
        {
            if (root.TryGetProperty("payload", out var payload) &&
                payload.TryGetProperty("payment", out var p) &&
                p.TryGetProperty("entity", out var entity) &&
                entity.TryGetProperty("id", out var pid))
                return pid.GetString();
        }
        catch { /* ignore */ }
        return null;
    }
}
