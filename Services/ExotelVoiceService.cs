using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TelecallingCRM.Data;

namespace TelecallingCRM.Services;

public interface IExotelVoiceService
{
    /// <summary>
    /// Initiates a Click-to-Call: Exotel first calls the agent's phone,
    /// then bridges to the lead's phone. Returns the Exotel call SID.
    /// </summary>
    Task<(bool success, string? callSid, string? error)> ClickToCallAsync(
        Guid tenantId, string agentPhone, string leadPhone, string? callerId = null);

    /// <summary>Returns true if an Exotel integration is enabled for this tenant.</summary>
    Task<bool> IsConfiguredAsync(Guid tenantId);
}

public class ExotelVoiceService : IExotelVoiceService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<ExotelVoiceService> _logger;
    private readonly IConfiguration _config;

    public ExotelVoiceService(AppDbContext db, IHttpClientFactory http,
        ILogger<ExotelVoiceService> logger, IConfiguration config)
    {
        _db = db;
        _http = http;
        _logger = logger;
        _config = config;
    }

    public async Task<(bool success, string? callSid, string? error)> ClickToCallAsync(
        Guid tenantId, string agentPhone, string leadPhone, string? callerId = null)
    {
        var cfg = await GetExotelCfgAsync(tenantId);
        if (cfg == null)
            return (false, null, "Exotel is not configured. Go to Admin ? Integrations ? Exotel.");

        if (!cfg.TryGetValue("ApiKey", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
            return (false, null, "Exotel ApiKey is missing.");
        if (!cfg.TryGetValue("ApiToken", out var apiToken) || string.IsNullOrWhiteSpace(apiToken))
            return (false, null, "Exotel ApiToken is missing.");
        if (!cfg.TryGetValue("AccountSid", out var accountSid) || string.IsNullOrWhiteSpace(accountSid))
            return (false, null, "Exotel AccountSid is missing.");

        // CallerId (ExoPhone) — use lead's caller ID field or fall back to config FromNumber
        var from = callerId
            ?? (cfg.TryGetValue("FromNumber", out var fn) ? fn : null)
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(from))
            return (false, null, "Exotel FromNumber (ExoPhone) is missing.");

        try
        {
            var client = _http.CreateClient("exotel");

            // Basic auth: ApiKey:ApiToken
            var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{apiToken}"));
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", creds);

            // Exotel supports two API base URL formats:
            //   Old accounts : https://api.exotel.com/v1/Accounts/{sid}/Calls/connect.json
            //   New accounts : https://{sid}.api.exotel.com/v1/Accounts/{sid}/Calls/connect.json
            // The subdomain format is the current standard and works for both.
            var url = $"https://{accountSid}.api.exotel.com/v1/Accounts/{accountSid}/Calls/connect.json";

            // StatusCallback — Exotel will POST call result (duration, recording) back here
            var appBaseUrl = _config["AppBaseUrl"]?.TrimEnd('/')
                ?? string.Empty;
            var statusCallbackUrl = string.IsNullOrEmpty(appBaseUrl)
                ? null
                : $"{appBaseUrl}/api/dialer/callback";

            var formParams = new Dictionary<string, string>
            {
                ["From"]      = agentPhone,  // agent's phone — Exotel calls this first
                ["To"]        = leadPhone,   // lead's phone — connected after agent picks up
                ["CallerId"]  = from,        // your Exotel ExoPhone number
                ["Record"]    = "true",
                ["TimeLimit"] = "3600",      // max 1 hour
                ["TimeOut"]   = "30",        // ring timeout seconds
            };

            if (!string.IsNullOrEmpty(statusCallbackUrl))
                formParams["StatusCallback"] = statusCallbackUrl;

            var formData = new FormUrlEncodedContent(formParams);

            var response = await client.PostAsync(url, formData);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Exotel Click-to-Call failed: {Status} {Body}", response.StatusCode, body);
                return (false, null, $"Exotel error {(int)response.StatusCode}: {body}");
            }

            // Parse Sid from response JSON
            using var doc = JsonDocument.Parse(body);
            var sid = doc.RootElement
                .GetProperty("Call")
                .GetProperty("Sid")
                .GetString();

            _logger.LogInformation("Exotel call initiated: Sid={Sid}, Agent={Agent}, Lead={Lead}",
                sid, agentPhone, leadPhone);

            return (true, sid, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exotel ClickToCall exception for tenant {TenantId}", tenantId);
            return (false, null, ex.Message);
        }
    }

    public async Task<bool> IsConfiguredAsync(Guid tenantId)
        => await _db.IntegrationConfigs
            .AnyAsync(i => i.TenantId == tenantId && i.Provider == "exotel" && i.IsEnabled);

    private async Task<Dictionary<string, string>?> GetExotelCfgAsync(Guid tenantId)
    {
        var cfg = await _db.IntegrationConfigs
            .FirstOrDefaultAsync(i => i.TenantId == tenantId
                                   && i.Provider == "exotel"
                                   && i.IsEnabled);
        if (cfg == null) return null;
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(cfg.ConfigJson); }
        catch { return null; }
    }
}
