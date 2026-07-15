using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Pages;

public class ForgotPasswordModel : PageModel
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<ForgotPasswordModel> _logger;

    public ForgotPasswordModel(UserManager<AppUser> userManager, ILogger<ForgotPasswordModel> logger)
    {
        _userManager = userManager;
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
        if (user == null || !await _userManager.IsEmailConfirmedAsync(user))
        {
            EmailSent = true;
            return Page();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetUrl = Url.Page("/ResetPassword",
            pageHandler: null,
            values: new { token = Uri.EscapeDataString(token), email = Input.Email },
            protocol: Request.Scheme)!;

        // TODO: Send via configured email integration (SMTP/SendGrid)
        // For now, log it so admins can provide the link manually
        _logger.LogInformation("Password reset link for {Email}: {Url}", Input.Email, resetUrl);

        EmailSent = true;
        return Page();
    }
}
