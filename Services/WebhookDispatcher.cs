using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Services;

/// <summary>
/// Dispatches webhook events to all active tenant webhook endpoints.
/// Called from API endpoints via Hangfire background job so HTTP delivery
/// never blocks the main request pipeline.
/// </summary>
public interface IWebhookDispatcher
{
    Task FireAsync(Guid tenantId, WebhookEvent eventType, object payload);
    Task DeliverAsync(Guid webhookId, string eventName, object payload);
}

public class WebhookDispatcher : IWebhookDispatcher
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<WebhookDispatcher> _logger;

    public WebhookDispatcher(AppDbContext db, IHttpClientFactory http, ILogger<WebhookDispatcher> logger)
    {
        _db = db;
        _http = http;
        _logger = logger;
    }

    public async Task FireAsync(Guid tenantId, WebhookEvent eventType, object payload)
    {
        var hooks = await _db.WebhookConfigs
            .Where(w => w.TenantId == tenantId && w.IsActive)
            .ToListAsync();

        foreach (var hook in hooks)
        {
            // Check if this hook subscribes to this event
            List<string>? events = null;
            try { events = JsonSerializer.Deserialize<List<string>>(hook.Events); } catch { }
            if (events != null && !events.Contains(eventType.ToString())) continue;

            // Queue each delivery as a Hangfire background job for resilience
            BackgroundJob.Enqueue<IWebhookDispatcher>(d =>
                d.DeliverAsync(hook.Id, eventType.ToString(), payload));
        }
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 300 })]
    public async Task DeliverAsync(Guid webhookId, string eventName, object payload)
    {
        var hook = await _db.WebhookConfigs.FindAsync(webhookId);
        if (hook == null || !hook.IsActive) return;

        var body = JsonSerializer.Serialize(new {
            @event = eventName,
            tenantId = hook.TenantId,
            timestamp = DateTime.UtcNow,
            data = payload
        });

        var signature = string.IsNullOrEmpty(hook.Secret) ? null
            : "sha256=" + ComputeHmacSha256(body, hook.Secret);

        try
        {
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var request = new HttpRequestMessage(HttpMethod.Post, hook.Url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            if (signature != null)
                request.Headers.Add("X-TelecallingCRM-Signature", signature);
            request.Headers.Add("X-TelecallingCRM-Event", eventName);

            var res = await client.SendAsync(request);
            hook.LastTriggeredAt = DateTime.UtcNow;

            var log = new WebhookDeliveryLog {
                WebhookId = webhookId, EventName = eventName,
                HttpStatus = (int)res.StatusCode, Success = res.IsSuccessStatusCode
            };

            if (!res.IsSuccessStatusCode)
            {
                hook.FailureCount++;
                log.ErrorMessage = await res.Content.ReadAsStringAsync();
                _logger.LogWarning("Webhook {Id} returned {Status}", webhookId, res.StatusCode);
            }
            else
            {
                hook.FailureCount = 0;
            }
            _db.WebhookDeliveryLogs.Add(log);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            hook.FailureCount++;
            _db.WebhookDeliveryLogs.Add(new WebhookDeliveryLog {
                WebhookId = webhookId, EventName = eventName,
                HttpStatus = 0, Success = false, ErrorMessage = ex.Message
            });
            await _db.SaveChangesAsync();
            _logger.LogError(ex, "Webhook {Id} delivery failed", webhookId);
            throw; // allows Hangfire to retry
        }
    }

    private static string ComputeHmacSha256(string data, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLower();
    }
}
