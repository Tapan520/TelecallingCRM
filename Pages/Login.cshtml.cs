using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Pages;

public class LoginModel : PageModel
{
    private readonly SignInManager<AppUser> _signInManager;
    private readonly UserManager<AppUser> _userManager;
    private readonly IUserActivityLogger _logger;

    public LoginModel(SignInManager<AppUser> signInManager, UserManager<AppUser> userManager, IUserActivityLogger logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!ModelState.IsValid) return Page();

        var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, isPersistent: true, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user != null)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                if (user.TenantId.HasValue)
                    await _logger.LogAsync(user.TenantId.Value, user.Id, "Login", "Auth",
                        $"{user.FullName} ({user.Role}) logged in",
                        ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                        userAgent: Request.Headers["User-Agent"].ToString());

                if (user.Role == "superadmin")
                    return RedirectToPage("/SuperAdmin/Tenants");
            }
            return LocalRedirect(returnUrl ?? "/Dashboard");
        }

        if (result.IsLockedOut)
        {
            ErrorMessage = "Account locked due to too many failed attempts. Try again in 5 minutes.";
            return Page();
        }

        ErrorMessage = "Invalid email or password.";
        return Page();
    }
}
