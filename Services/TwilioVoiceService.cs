using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Twilio;
using Twilio.Jwt.AccessToken;
using Twilio.Rest.Api.V2010.Account;
using TelecallingCRM.Data;

namespace TelecallingCRM.Services;

public interface ITwilioVoiceService
{
    /// <summary>Generates a Twilio Access Token for the browser JS SDK.</summary>
    Task<(string? token, string? error)> GenerateAccessTokenAsync(Guid tenantId, string agentIdentity);

    /// <summary>Initiates an outbound call via Twilio REST API (non-browser path).</summary>
    Task<(bool success, string? callSid, string? error)> InitiateCallAsync(Guid tenantId, string toPhone, string agentIdentity);
}

public class TwilioVoiceService : ITwilioVoiceService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TwilioVoiceService> _logger;

    public TwilioVoiceService(AppDbContext db, ILogger<TwilioVoiceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<(string? token, string? error)> GenerateAccessTokenAsync(Guid tenantId, string agentIdentity)
    {
        var cfg = await GetTwilioCfgAsync(tenantId);
        if (cfg == null) return (null, "Twilio is not configured. Go to Admin ? Integrations and configure Twilio.");

        if (!cfg.TryGetValue("TwimlAppSid", out var twimlAppSid) || string.IsNullOrWhiteSpace(twimlAppSid))
            return (null, "TwimlAppSid is missing in Twilio configuration.");

        // Use ApiKey + ApiSecret if provided, otherwise fall back to AccountSid + AuthToken
        var signingKeySid    = cfg.GetValueOrDefault("ApiKeySid",    cfg["AccountSid"]);
        var signingKeySecret = cfg.GetValueOrDefault("ApiKeySecret", cfg["AuthToken"]);

        var grant = new VoiceGrant
        {
            OutgoingApplicationSid = twimlAppSid,
            IncomingAllow = true
        };

        var token = new Token(
            cfg["AccountSid"],
            signingKeySid,
            signingKeySecret,
            agentIdentity,
            expiration: DateTime.UtcNow.AddHours(1),
            grants: new HashSet<IGrant> { grant });

        return (token.ToJwt(), null);
    }

    public async Task<(bool success, string? callSid, string? error)> InitiateCallAsync(
        Guid tenantId, string toPhone, string agentIdentity)
    {
        var cfg = await GetTwilioCfgAsync(tenantId);
        if (cfg == null) return (false, null, "Twilio not configured.");

        if (!cfg.TryGetValue("TwimlAppSid", out var twimlAppSid) || string.IsNullOrWhiteSpace(twimlAppSid))
            return (false, null, "TwimlAppSid missing in Twilio config.");

        try
        {
            TwilioClient.Init(cfg["AccountSid"], cfg["AuthToken"]);
            var call = await CallResource.CreateAsync(
                to:   new Twilio.Types.PhoneNumber(toPhone),
                from: new Twilio.Types.PhoneNumber(cfg["FromNumber"]),
                applicationSid: twimlAppSid);

            return (true, call.Sid, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Twilio call initiation failed for tenant {TenantId}", tenantId);
            return (false, null, ex.Message);
        }
    }

    private async Task<Dictionary<string, string>?> GetTwilioCfgAsync(Guid tenantId)
    {
        var cfg = await _db.IntegrationConfigs
            .FirstOrDefaultAsync(i => i.TenantId == tenantId
                                   && i.Provider == "twilio"
                                   && i.IsEnabled);
        if (cfg == null) return null;
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(cfg.ConfigJson); }
        catch { return null; }
    }
}
