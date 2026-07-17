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
    private readonly ILogger<ForgotPasswordModel> _logger;

    public ForgotPasswordModel(UserManager<AppUser> userManager,
        IMessageDispatcher email,
        ILogger<ForgotPasswordModel> logger)
    {
        _userManager = userManager;
        _email = email;
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
        // Always show success to prevent user enumeration
        if (user == null)
        {
            EmailSent = true;
            return Page();
        }

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
                    <a href="{resetUrl}"
                       style="background:#4f46e5;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold;">
                        Reset Password
                    </a>
                </p>
                <p style="font-size:12px;color:#9ca3af;">If you did not request this, please ignore this email. Your password will remain unchanged.</p>
                <hr style="border:none;border-top:1px solid #e5e7eb;margin:24px 0;">
                <p style="font-size:11px;color:#9ca3af;">TelecallingCRM · Powered by your team</p>
            </div>
            """;

        // Try sending via configured email integration; fall back to logging
        if (user.TenantId.HasValue)
        {
            var (ok, err) = await _email.SendEmailAsync(
                user.TenantId.Value, Input.Email,
                "Reset your TelecallingCRM password", htmlBody);

            if (!ok)
                _logger.LogWarning("Failed to send password reset email to {Email}: {Err}", Input.Email, err);
        }

        // Always log for admin fallback
        _logger.LogInformation("Password reset link for {Email}: {Url}", Input.Email, resetUrl);

        EmailSent = true;
        return Page();
    }
}

