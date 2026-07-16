using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Services;

public interface ICrmSyncService
{
    Task SyncLeadsToHubSpotAsync(Guid tenantId, CancellationToken ct = default);
    Task SyncLeadsToSalesforceAsync(Guid tenantId, CancellationToken ct = default);
    Task PullContactsFromHubSpotAsync(Guid tenantId, CancellationToken ct = default);
}

/// <summary>
/// Pushes / pulls lead data between TelecallingCRM and HubSpot / Salesforce.
/// Uses stored CrmSyncConfig credentials. Access-token refresh is handled inline.
/// </summary>
public class CrmSyncService : ICrmSyncService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<CrmSyncService> _logger;

    public CrmSyncService(AppDbContext db, IHttpClientFactory httpFactory, ILogger<CrmSyncService> logger)
    {
        _db = db;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    // ------------------------------------------------------------------ HubSpot

    public async Task SyncLeadsToHubSpotAsync(Guid tenantId, CancellationToken ct = default)
    {
        var config = await GetActiveConfigAsync(tenantId, "hubspot", ct);
        if (config == null) return;

        var leads = await _db.Leads
            .Where(l => l.TenantId == tenantId && l.Status != LeadStatus.Dead)
            .Take(100)
            .ToListAsync(ct);

        var client = _httpFactory.CreateClient("hubspot");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", config.AccessToken);

        foreach (var lead in leads)
        {
            try
            {
                var payload = new
                {
                    properties = new
                    {
                        firstname = lead.Name.Split(' ').First(),
                        lastname  = lead.Name.Contains(' ') ? lead.Name[(lead.Name.IndexOf(' ') + 1)..] : "",
                        phone     = lead.Phone,
                        email     = lead.Email ?? "",
                        company   = lead.Company ?? "",
                        hs_lead_status = MapStatus(lead.Status)
                    }
                };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var resp = await client.PostAsync("https://api.hubapi.com/crm/v3/objects/contacts", content, ct);

                var success = resp.IsSuccessStatusCode;
                var extId = string.Empty;
                if (success)
                {
                    var body = await resp.Content.ReadAsStringAsync(ct);
                    var doc = JsonDocument.Parse(body);
                    extId = doc.RootElement.GetProperty("id").GetString() ?? string.Empty;
                }

                _db.CrmSyncLogs.Add(new CrmSyncLog
                {
                    TenantId = tenantId,
                    CrmSyncConfigId = config.Id,
                    Provider = "hubspot",
                    ObjectType = "Contact",
                    Direction = "push",
                    ExternalId = extId,
                    LocalLeadId = lead.Id,
                    Success = success,
                    ErrorMessage = success ? null : $"HTTP {(int)resp.StatusCode}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HubSpot sync failed for lead {LeadId}", lead.Id);
                _db.CrmSyncLogs.Add(new CrmSyncLog
                {
                    TenantId = tenantId, CrmSyncConfigId = config.Id,
                    Provider = "hubspot", ObjectType = "Contact", Direction = "push",
                    ExternalId = "", LocalLeadId = lead.Id,
                    Success = false, ErrorMessage = ex.Message
                });
            }
        }

        config.LastSyncedAt = DateTime.UtcNow;
        config.LastSyncStatus = "ok";
        config.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("HubSpot sync completed for tenant {TenantId}: {Count} leads.", tenantId, leads.Count);
    }

    public async Task PullContactsFromHubSpotAsync(Guid tenantId, CancellationToken ct = default)
    {
        var config = await GetActiveConfigAsync(tenantId, "hubspot", ct);
        if (config == null) return;

        var client = _httpFactory.CreateClient("hubspot");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", config.AccessToken);

        var resp = await client.GetAsync(
            "https://api.hubapi.com/crm/v3/objects/contacts?limit=50&properties=firstname,lastname,phone,email,company", ct);
        if (!resp.IsSuccessStatusCode) return;

        var body = await resp.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(body);
        var results = doc.RootElement.GetProperty("results");

        foreach (var contact in results.EnumerateArray())
        {
            var props = contact.GetProperty("properties");
            var phone = props.TryGetProperty("phone", out var ph) ? ph.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(phone)) continue;

            var exists = await _db.Leads.AnyAsync(l => l.TenantId == tenantId && l.Phone == phone, ct);
            if (exists) continue;

            var firstName = props.TryGetProperty("firstname", out var fn) ? fn.GetString() ?? "" : "";
            var lastName  = props.TryGetProperty("lastname",  out var ln) ? ln.GetString() ?? "" : "";
            var name = $"{firstName} {lastName}".Trim();
            if (string.IsNullOrWhiteSpace(name)) name = phone;

            var newLead = new Lead
            {
                TenantId = tenantId,
                Name     = name,
                Phone    = phone,
                Email    = props.TryGetProperty("email",   out var em) ? em.GetString() : null,
                Company  = props.TryGetProperty("company", out var co) ? co.GetString() : null,
                Source   = "HubSpot"
            };
            _db.Leads.Add(newLead);

            var extId = contact.TryGetProperty("id", out var cid) ? cid.GetString() ?? "" : "";
            _db.CrmSyncLogs.Add(new CrmSyncLog
            {
                TenantId = tenantId, CrmSyncConfigId = config.Id,
                Provider = "hubspot", ObjectType = "Contact", Direction = "pull",
                ExternalId = extId, LocalLeadId = newLead.Id, Success = true
            });
        }

        config.LastSyncedAt = DateTime.UtcNow;
        config.LastSyncStatus = "ok";
        config.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------ Salesforce

    public async Task SyncLeadsToSalesforceAsync(Guid tenantId, CancellationToken ct = default)
    {
        var config = await GetActiveConfigAsync(tenantId, "salesforce", ct);
        if (config == null) return;

        var leads = await _db.Leads
            .Where(l => l.TenantId == tenantId && l.Status != LeadStatus.Dead)
            .Take(100)
            .ToListAsync(ct);

        var client = _httpFactory.CreateClient("salesforce");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", config.AccessToken);

        var instanceUrl = config.InstanceUrl?.TrimEnd('/') ?? "https://login.salesforce.com";

        foreach (var lead in leads)
        {
            try
            {
                var payload = new
                {
                    LastName  = lead.Name,
                    Phone     = lead.Phone,
                    Email     = lead.Email,
                    Company   = lead.Company ?? "Unknown",
                    LeadSource = lead.Source ?? "TelecallingCRM",
                    Status    = MapStatus(lead.Status)
                };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var resp = await client.PostAsync($"{instanceUrl}/services/data/v58.0/sobjects/Lead", content, ct);

                var success = resp.IsSuccessStatusCode;
                var extId = string.Empty;
                if (success)
                {
                    var bodyStr = await resp.Content.ReadAsStringAsync(ct);
                    var doc = JsonDocument.Parse(bodyStr);
                    extId = doc.RootElement.TryGetProperty("id", out var sfId) ? sfId.GetString() ?? "" : "";
                }

                _db.CrmSyncLogs.Add(new CrmSyncLog
                {
                    TenantId = tenantId, CrmSyncConfigId = config.Id,
                    Provider = "salesforce", ObjectType = "Lead", Direction = "push",
                    ExternalId = extId, LocalLeadId = lead.Id,
                    Success = success, ErrorMessage = success ? null : $"HTTP {(int)resp.StatusCode}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Salesforce sync failed for lead {LeadId}", lead.Id);
                _db.CrmSyncLogs.Add(new CrmSyncLog
                {
                    TenantId = tenantId, CrmSyncConfigId = config.Id,
                    Provider = "salesforce", ObjectType = "Lead", Direction = "push",
                    ExternalId = "", LocalLeadId = lead.Id,
                    Success = false, ErrorMessage = ex.Message
                });
            }
        }

        config.LastSyncedAt = DateTime.UtcNow;
        config.LastSyncStatus = "ok";
        config.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Salesforce sync completed for tenant {TenantId}: {Count} leads.", tenantId, leads.Count);
    }

    // ------------------------------------------------------------------ Helpers

    private async Task<CrmSyncConfig?> GetActiveConfigAsync(Guid tenantId, string provider, CancellationToken ct)
    {
        var config = await _db.CrmSyncConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Provider == provider && c.IsActive, ct);
        if (config == null)
            _logger.LogWarning("No active {Provider} sync config for tenant {TenantId}", provider, tenantId);
        return config;
    }

    private static string MapStatus(LeadStatus status) => status switch
    {
        LeadStatus.New          => "New",
        LeadStatus.Contacted    => "Contacted",
        LeadStatus.Interested   => "Working",
        LeadStatus.NotInterested => "Unqualified",
        LeadStatus.FollowUp     => "Working",
        LeadStatus.Converted    => "Converted",
        LeadStatus.Dead         => "Dead",
        _                       => "New"
    };
}
