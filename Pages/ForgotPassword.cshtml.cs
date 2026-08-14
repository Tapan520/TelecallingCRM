using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Pages;

public class ForgotPasswordModel : PageModel
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IMessageDispatcher _email;
    private readonly IConfiguration _cfg;
    private readonly ILogger<ForgotPasswordModel> _logger;

    public ForgotPasswordModel(UserManager<AppUser> userManager,
        IMessageDispatcher email,
        IConfiguration cfg,
        ILogger<ForgotPasswordModel> logger)
    {
        _userManager = userManager;
        _email = email;
        _cfg = cfg;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool EmailSent { get; set; }
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await _userManager.FindByEmailAsync(Input.Email);
        if (user == null) { EmailSent = true; return Page(); }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetUrl = Url.Page("/ResetPassword",
            pageHandler: null,
            values: new { token = Uri.EscapeDataString(token), email = Input.Email },
            protocol: Request.Scheme)!;

        var htmlBody = $"""
            <div style="font-family:Arial,sans-serif;max-width:480px;margin:auto;padding:32px;background:#f8fafc;border-radius:12px;">
                <h2 style="color:#4f46e5;margin-bottom:8px;">TelecallingCRM</h2>
                <h3>Password Reset Request</h3>
                <p>Hi <strong>{user.FullName}</strong>,</p>
                <p>We received a request to reset your password. Click the button below to set a new password. This link expires in 24 hours.</p>
                <p style="margin:24px 0;">
                    <a href="{resetUrl}" style="background:#4f46e5;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold;">
                        Reset Password
                    </a>
                </p>
                <p style="font-size:12px;color:#9ca3af;">If you did not request this, please ignore this email.</p>
            </div>
            """;

        bool sent = false;

        // 1. Try tenant-configured email integration (Twilio/SendGrid/SMTP)
        if (user.TenantId.HasValue)
        {
            var (ok, _) = await _email.SendEmailAsync(
                user.TenantId.Value, Input.Email,
                "Reset your TelecallingCRM password", htmlBody);
            sent = ok;
        }

        // 2. Fall back to system-level SMTP from appsettings.json
        if (!sent)
            sent = await TrySendViaSystemSmtpAsync(Input.Email, htmlBody);

        if (!sent)
            _logger.LogWarning("Password reset email could not be sent for {Email}. Reset URL: {Url}", Input.Email, resetUrl);

        EmailSent = true;
        return Page();
    }

    private async Task<bool> TrySendViaSystemSmtpAsync(string toEmail, string htmlBody)
    {
        // System-level fallback using appsettings / Railway env vars.
        // Tries Resend first (HTTPS), then Mailjet (HTTPS), then SMTP.

        // System Resend (env: SystemEmail__ResendApiKey, SystemEmail__ResendFromEmail)
        var resendKey = _cfg["SystemEmail:ResendApiKey"] ?? _cfg["SystemEmail__ResendApiKey"];
        if (!string.IsNullOrWhiteSpace(resendKey))
        {
            try
            {
                var http = new System.Net.Http.HttpClient();
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", resendKey);
                var fromEmail = _cfg["SystemEmail:ResendFromEmail"] ?? _cfg["SystemEmail__ResendFromEmail"] ?? "noreply@telecallingcrm.app";
                var fromName  = _cfg["SystemEmail:FromName"] ?? _cfg["SystemEmail__FromName"] ?? "TelecallingCRM";
                var payload   = System.Text.Json.JsonSerializer.Serialize(new {
                    from    = $"{fromName} <{fromEmail}>",
                    to      = new[] { toEmail },
                    subject = "Reset your TelecallingCRM password",
                    html    = htmlBody
                });
                var res = await http.PostAsync("https://api.resend.com/emails",
                    new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
                if (res.IsSuccessStatusCode)
                {
                    _logger.LogInformation("[ForgotPassword][SystemResend] Sent to {Email}", toEmail);
                    return true;
                }
                _logger.LogWarning("[ForgotPassword][SystemResend] {Status}: {Body}", res.StatusCode, await res.Content.ReadAsStringAsync());
            }
            catch (Exception ex) { _logger.LogError(ex, "[ForgotPassword][SystemResend] error"); }
        }

        // System Mailjet (env: SystemEmail__MailjetApiKey, SystemEmail__MailjetSecretKey, SystemEmail__MailjetFromEmail)
        var mailjetKey    = _cfg["SystemEmail:MailjetApiKey"]    ?? _cfg["SystemEmail__MailjetApiKey"];
        var mailjetSecret = _cfg["SystemEmail:MailjetSecretKey"] ?? _cfg["SystemEmail__MailjetSecretKey"];
        var mailjetFrom   = _cfg["SystemEmail:MailjetFromEmail"] ?? _cfg["SystemEmail__MailjetFromEmail"];
        if (!string.IsNullOrWhiteSpace(mailjetKey) && !string.IsNullOrWhiteSpace(mailjetSecret) && !string.IsNullOrWhiteSpace(mailjetFrom))
        {
            try
            {
                var http  = new System.Net.Http.HttpClient();
                var creds = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{mailjetKey}:{mailjetSecret}"));
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", creds);
                var fromName = _cfg["SystemEmail:FromName"] ?? _cfg["SystemEmail__FromName"] ?? "TelecallingCRM";
                var payload  = System.Text.Json.JsonSerializer.Serialize(new {
                    Messages = new[] {
                        new {
                            From     = new { Email = mailjetFrom, Name = fromName },
                            To       = new[] { new { Email = toEmail, Name = toEmail } },
                            Subject  = "Reset your TelecallingCRM password",
                            HTMLPart = htmlBody
                        }
                    }
                });
                var res = await http.PostAsync("https://api.mailjet.com/v3.1/send",
                    new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
                if (res.IsSuccessStatusCode)
                {
                    _logger.LogInformation("[ForgotPassword][SystemMailjet] Sent to {Email}", toEmail);
                    return true;
                }
                _logger.LogWarning("[ForgotPassword][SystemMailjet] {Status}: {Body}", res.StatusCode, await res.Content.ReadAsStringAsync());
            }
            catch (Exception ex) { _logger.LogError(ex, "[ForgotPassword][SystemMailjet] error"); }
        }

        // System SMTP fallback (may be blocked on Railway)
        var host = _cfg["SystemEmail:Host"] ?? _cfg["SystemEmail__Host"];
        if (string.IsNullOrWhiteSpace(host)) return false;
        try
        {
            using var smtp = new System.Net.Mail.SmtpClient(host,
                int.TryParse(_cfg["SystemEmail:Port"] ?? _cfg["SystemEmail__Port"], out var p) ? p : 587)
            {
                Credentials = new System.Net.NetworkCredential(
                    _cfg["SystemEmail:Username"] ?? _cfg["SystemEmail__Username"],
                    _cfg["SystemEmail:Password"] ?? _cfg["SystemEmail__Password"]),
                EnableSsl = true
            };
            var fromEmail = _cfg["SystemEmail:FromEmail"] ?? _cfg["SystemEmail__FromEmail"] ?? "noreply@telecallingcrm.app";
            var fromName  = _cfg["SystemEmail:FromName"]  ?? _cfg["SystemEmail__FromName"]  ?? "TelecallingCRM";
            var mail = new System.Net.Mail.MailMessage(
                new System.Net.Mail.MailAddress(fromEmail, fromName),
                new System.Net.Mail.MailAddress(toEmail))
            {
                Subject = "Reset your TelecallingCRM password",
                Body    = htmlBody,
                IsBodyHtml = true
            };
            await smtp.SendMailAsync(mail);
            _logger.LogInformation("[ForgotPassword][SystemSMTP] Sent to {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ForgotPassword][SystemSMTP] Failed for {Email}", toEmail);
            return false;
        }
    }
}
