using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;

namespace TelecallingCRM.Services;

/// <summary>
/// Reads the tenant's IntegrationConfig for SMS/email providers and dispatches
/// messages via the configured provider.
///
/// Email provider priority (first enabled config wins):
///   1. Resend   — HTTPS, works on Railway (no SMTP port needed)
///   2. Mailjet  — HTTPS, works on Railway (no SMTP port needed)
///   3. SendGrid — HTTPS
///   4. SMTP     — may be blocked on Railway; use Resend/Mailjet for production
/// </summary>
public interface IMessageDispatcher
{
    Task<(bool success, string? error)> SendSmsAsync(Guid tenantId, string toPhone, string body);
    Task<(bool success, string? error)> SendEmailAsync(Guid tenantId, string toEmail, string subject, string htmlBody);
    Task<(bool success, string? error)> SendWhatsAppAsync(Guid tenantId, string toPhone, string body, string? templateId);
}

public class MessageDispatcher : IMessageDispatcher
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<MessageDispatcher> _logger;
    private readonly IConfiguration _cfg;

    public MessageDispatcher(AppDbContext db, IHttpClientFactory http, ILogger<MessageDispatcher> logger, IConfiguration cfg)
    {
        _db = db;
        _http = http;
        _logger = logger;
        _cfg = cfg;
    }

    // ?? SMS ??????????????????????????????????????????????????????????????????
    public async Task<(bool, string?)> SendSmsAsync(Guid tenantId, string toPhone, string body)
    {
        var cfg = await GetConfigAsync(tenantId, "twilio")
                  ?? await GetConfigAsync(tenantId, "exotel");

        if (cfg == null)
        {
            _logger.LogWarning("No SMS provider configured for tenant {TenantId}", tenantId);
            return (false, "No SMS provider configured");
        }

        if (cfg.ContainsKey("AccountSid") && cfg.ContainsKey("AuthToken"))
            return await SendViaTwilioSmsAsync(cfg, toPhone, body);

        return (false, "Unknown SMS provider config");
    }

    // ?? Email: Resend ? Mailjet ? SendGrid ? SMTP ????????????????????????????
    public async Task<(bool, string?)> SendEmailAsync(Guid tenantId, string toEmail, string subject, string htmlBody)
    {
        // 1. Resend (recommended for Railway — pure HTTPS)
        var resendCfg = await GetConfigAsync(tenantId, "resend");
        if (resendCfg != null && resendCfg.ContainsKey("ApiKey"))
            return await SendViaResendAsync(resendCfg, toEmail, subject, htmlBody);

        // 2. Mailjet (recommended for Railway — pure HTTPS)
        var mailjetCfg = await GetConfigAsync(tenantId, "mailjet");
        if (mailjetCfg != null && mailjetCfg.ContainsKey("ApiKey") && mailjetCfg.ContainsKey("SecretKey"))
            return await SendViaMailjetAsync(mailjetCfg, toEmail, subject, htmlBody);

        // 3. SendGrid
        var sgCfg = await GetConfigAsync(tenantId, "sendgrid");
        if (sgCfg != null && sgCfg.ContainsKey("ApiKey"))
            return await SendViaSendGridAsync(sgCfg, toEmail, subject, htmlBody);

        // 4. SMTP fallback (may be blocked on Railway)
        var smtpCfg = await GetConfigAsync(tenantId, "smtp");
        if (smtpCfg != null && smtpCfg.ContainsKey("Host"))
            return await SendViaSmtpAsync(smtpCfg, toEmail, subject, htmlBody);

        // 5. System-level fallback from Railway env vars (SystemEmail__*)
        var sysResult = await TrySendViaSystemEmailAsync(toEmail, subject, htmlBody);
        if (sysResult.HasValue) return sysResult.Value;

        _logger.LogWarning("No email provider configured for tenant {TenantId}", tenantId);
        return (false, "No email provider configured");
    }

    // ?? System-level email fallback (Railway env vars) ??????????????????????????????????
    private async Task<(bool, string?)?> TrySendViaSystemEmailAsync(string toEmail, string subject, string htmlBody)
    {
        // Try Resend first
        var resendKey = _cfg["SystemEmail:ResendApiKey"] ?? _cfg["SystemEmail__ResendApiKey"];
        if (!string.IsNullOrWhiteSpace(resendKey))
        {
            try
            {
                var client = _http.CreateClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", resendKey);
                var fromEmail = _cfg["SystemEmail:ResendFromEmail"] ?? _cfg["SystemEmail__ResendFromEmail"] ?? "noreply@telecallingcrm.app";
                var fromName  = _cfg["SystemEmail:FromName"] ?? _cfg["SystemEmail__FromName"] ?? "TelecallingCRM";
                var payload   = JsonSerializer.Serialize(new {
                    from    = $"{fromName} <{fromEmail}>",
                    to      = new[] { toEmail },
                    subject,
                    html    = htmlBody
                });
                var res = await client.PostAsync("https://api.resend.com/emails",
                    new StringContent(payload, Encoding.UTF8, "application/json"));
                var body = await res.Content.ReadAsStringAsync();
                if (res.IsSuccessStatusCode)
                {
                    _logger.LogInformation("[Email][SystemResend] Sent '{Subject}' to {Email}", subject, toEmail);
                    return (true, null);
                }
                _logger.LogWarning("[Email][SystemResend] {Status}: {Body}", res.StatusCode, body);
            }
            catch (Exception ex) { _logger.LogError(ex, "[Email][SystemResend] error"); }
        }

        // Try Mailjet
        var mailjetKey    = _cfg["SystemEmail:MailjetApiKey"]    ?? _cfg["SystemEmail__MailjetApiKey"];
        var mailjetSecret = _cfg["SystemEmail:MailjetSecretKey"] ?? _cfg["SystemEmail__MailjetSecretKey"];
        var mailjetFrom   = _cfg["SystemEmail:MailjetFromEmail"] ?? _cfg["SystemEmail__MailjetFromEmail"];
        if (!string.IsNullOrWhiteSpace(mailjetKey) && !string.IsNullOrWhiteSpace(mailjetSecret) && !string.IsNullOrWhiteSpace(mailjetFrom))
        {
            try
            {
                var client = _http.CreateClient();
                var creds  = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{mailjetKey}:{mailjetSecret}"));
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", creds);
                var fromName = _cfg["SystemEmail:FromName"] ?? _cfg["SystemEmail__FromName"] ?? "TelecallingCRM";
                var payload  = JsonSerializer.Serialize(new {
                    Messages = new[] {
                        new {
                            From     = new { Email = mailjetFrom, Name = fromName },
                            To       = new[] { new { Email = toEmail, Name = toEmail } },
                            Subject  = subject,
                            HTMLPart = htmlBody
                        }
                    }
                });
                var res = await client.PostAsync("https://api.mailjet.com/v3.1/send",
                    new StringContent(payload, Encoding.UTF8, "application/json"));
                var body = await res.Content.ReadAsStringAsync();
                if (res.IsSuccessStatusCode)
                {
                    _logger.LogInformation("[Email][SystemMailjet] Sent '{Subject}' to {Email}", subject, toEmail);
                    return (true, null);
                }
                _logger.LogWarning("[Email][SystemMailjet] {Status}: {Body}", res.StatusCode, body);
            }
            catch (Exception ex) { _logger.LogError(ex, "[Email][SystemMailjet] error"); }
        }

        return null;
    }

    // ?? WhatsApp ?????????????????????????????????????????????????????????????
    public async Task<(bool, string?)> SendWhatsAppAsync(Guid tenantId, string toPhone, string body, string? templateId)
    {
        var cfg = await GetConfigAsync(tenantId, "twilio")
                  ?? await GetConfigAsync(tenantId, "whatsapp_waba");
        if (cfg == null) return (false, "No WhatsApp provider configured");
        if (cfg.ContainsKey("AccountSid")) return await SendViaTwilioWhatsAppAsync(cfg, toPhone, body);
        if (cfg.ContainsKey("PhoneNumberId")) return await SendViaWabaAsync(cfg, toPhone, body, templateId);
        return (false, "Unknown WhatsApp config");
    }

    // ?? Resend ????????????????????????????????????????????????????????????????
    private async Task<(bool, string?)> SendViaResendAsync(Dictionary<string, string> cfg, string toEmail, string subject, string htmlBody)
    {
        try
        {
            var client = _http.CreateClient("resend");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", cfg["ApiKey"]);

            var fromEmail = cfg.GetValueOrDefault("FromEmail", "noreply@telecallingcrm.app");
            var fromName  = cfg.GetValueOrDefault("FromName", "TelecallingCRM");

            var payload = JsonSerializer.Serialize(new
            {
                from    = $"{fromName} <{fromEmail}>",
                to      = new[] { toEmail },
                subject = subject,
                html    = htmlBody
            });

            var res = await client.PostAsync("https://api.resend.com/emails",
                new StringContent(payload, Encoding.UTF8, "application/json"));
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("[Email][Resend] API error {Status}: {Body}", res.StatusCode, body);
                return (false, $"Resend: {body}");
            }

            _logger.LogInformation("[Email][Resend] Sent '{Subject}' to {Email}", subject, toEmail);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Email][Resend] Failed to send '{Subject}' to {Email}", subject, toEmail);
            return (false, ex.Message);
        }
    }

    // ?? Mailjet ???????????????????????????????????????????????????????????????
    private async Task<(bool, string?)> SendViaMailjetAsync(Dictionary<string, string> cfg, string toEmail, string subject, string htmlBody)
    {
        try
        {
            var client = _http.CreateClient("mailjet");
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{cfg["ApiKey"]}:{cfg["SecretKey"]}"));
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);

            var fromEmail = cfg.GetValueOrDefault("FromEmail", "noreply@telecallingcrm.app");
            var fromName  = cfg.GetValueOrDefault("FromName", "TelecallingCRM");

            var payload = JsonSerializer.Serialize(new
            {
                Messages = new[]
                {
                    new
                    {
                        From     = new { Email = fromEmail, Name = fromName },
                        To       = new[] { new { Email = toEmail, Name = toEmail } },
                        Subject  = subject,
                        HTMLPart = htmlBody
                    }
                }
            });

            var res = await client.PostAsync("https://api.mailjet.com/v3.1/send",
                new StringContent(payload, Encoding.UTF8, "application/json"));
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("[Email][Mailjet] API error {Status}: {Body}", res.StatusCode, body);
                return (false, $"Mailjet: {body}");
            }

            _logger.LogInformation("[Email][Mailjet] Sent '{Subject}' to {Email}", subject, toEmail);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Email][Mailjet] Failed to send '{Subject}' to {Email}", subject, toEmail);
            return (false, ex.Message);
        }
    }

    // ?? SendGrid ??????????????????????????????????????????????????????????????
    private async Task<(bool, string?)> SendViaSendGridAsync(Dictionary<string, string> cfg, string to, string subject, string htmlBody)
    {
        try
        {
            var client = _http.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", cfg["ApiKey"]);

            var payload = JsonSerializer.Serialize(new {
                personalizations = new[] { new { to = new[] { new { email = to } } } },
                from = new {
                    email = cfg.GetValueOrDefault("FromEmail", "noreply@telecallingcrm.app"),
                    name  = cfg.GetValueOrDefault("FromName",  "TelecallingCRM")
                },
                subject,
                content = new[] { new { type = "text/html", value = htmlBody } }
            });

            var res = await client.PostAsync("https://api.sendgrid.com/v3/mail/send",
                new StringContent(payload, Encoding.UTF8, "application/json"));
            return res.IsSuccessStatusCode
                ? (true, null)
                : (false, await res.Content.ReadAsStringAsync());
        }
        catch (Exception ex) { _logger.LogError(ex, "SendGrid error"); return (false, ex.Message); }
    }

    // ?? SMTP ??????????????????????????????????????????????????????????????????
    private async Task<(bool, string?)> SendViaSmtpAsync(Dictionary<string, string> cfg, string to, string subject, string htmlBody)
    {
        try
        {
            using var smtp = new System.Net.Mail.SmtpClient(
                cfg["Host"], int.Parse(cfg.GetValueOrDefault("Port", "587")))
            {
                Credentials = new System.Net.NetworkCredential(cfg["Username"], cfg["Password"]),
                EnableSsl   = true
            };
            var mail = new System.Net.Mail.MailMessage(
                new System.Net.Mail.MailAddress(
                    cfg.GetValueOrDefault("FromEmail", cfg["Username"]),
                    cfg.GetValueOrDefault("FromName",  "TelecallingCRM")),
                new System.Net.Mail.MailAddress(to))
            {
                Subject = subject, Body = htmlBody, IsBodyHtml = true
            };
            await smtp.SendMailAsync(mail);
            return (true, null);
        }
        catch (Exception ex) { _logger.LogError(ex, "SMTP error"); return (false, ex.Message); }
    }

    // ?? Twilio SMS ?????????????????????????????????????????????????????????????
    private async Task<(bool, string?)> SendViaTwilioSmsAsync(Dictionary<string, string> cfg, string to, string body)
    {
        try
        {
            var client = _http.CreateClient();
            var creds  = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{cfg["AccountSid"]}:{cfg["AuthToken"]}"));
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", creds);

            var form = new FormUrlEncodedContent(new Dictionary<string, string> {
                ["To"] = to, ["From"] = cfg["FromNumber"], ["Body"] = body
            });
            var res = await client.PostAsync(
                $"https://api.twilio.com/2010-04-01/Accounts/{cfg["AccountSid"]}/Messages.json", form);
            return res.IsSuccessStatusCode
                ? (true, null)
                : (false, await res.Content.ReadAsStringAsync());
        }
        catch (Exception ex) { _logger.LogError(ex, "Twilio SMS error"); return (false, ex.Message); }
    }

    // ?? Twilio WhatsApp ????????????????????????????????????????????????????????
    private async Task<(bool, string?)> SendViaTwilioWhatsAppAsync(Dictionary<string, string> cfg, string to, string body)
    {
        try
        {
            var client = _http.CreateClient();
            var creds  = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{cfg["AccountSid"]}:{cfg["AuthToken"]}"));
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", creds);

            var form = new FormUrlEncodedContent(new Dictionary<string, string> {
                ["To"] = $"whatsapp:{to}", ["From"] = $"whatsapp:{cfg["FromNumber"]}", ["Body"] = body
            });
            var res = await client.PostAsync(
                $"https://api.twilio.com/2010-04-01/Accounts/{cfg["AccountSid"]}/Messages.json", form);
            return res.IsSuccessStatusCode
                ? (true, null)
                : (false, await res.Content.ReadAsStringAsync());
        }
        catch (Exception ex) { _logger.LogError(ex, "Twilio WhatsApp error"); return (false, ex.Message); }
    }

    // ?? WhatsApp Business API (Meta WABA) ??????????????????????????????????????
    private async Task<(bool, string?)> SendViaWabaAsync(Dictionary<string, string> cfg, string to, string body, string? templateId)
    {
        try
        {
            var client = _http.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", cfg["AccessToken"]);

            var payload = JsonSerializer.Serialize(new {
                messaging_product = "whatsapp", to,
                type = "text", text = new { body }
            });
            var res = await client.PostAsync(
                $"https://graph.facebook.com/v18.0/{cfg["PhoneNumberId"]}/messages",
                new StringContent(payload, Encoding.UTF8, "application/json"));
            return res.IsSuccessStatusCode
                ? (true, null)
                : (false, await res.Content.ReadAsStringAsync());
        }
        catch (Exception ex) { _logger.LogError(ex, "WABA error"); return (false, ex.Message); }
    }

    // ?? Helper ????????????????????????????????????????????????????????????????
    private async Task<Dictionary<string, string>?> GetConfigAsync(Guid tenantId, string provider)
    {
        var cfg = await _db.IntegrationConfigs
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Provider == provider && i.IsEnabled);
        if (cfg == null) return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(cfg.ConfigJson);
        }
        catch { return null; }
    }
}
