using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Pages;

public class LoginModel : PageModel
{
    private readonly SignInManager<AppUser> _signInManager;
    private readonly UserManager<AppUser> _userManager;
    private readonly IUserActivityLogger _logger;
    private readonly AppDbContext _db;

    public LoginModel(SignInManager<AppUser> signInManager, UserManager<AppUser> userManager, IUserActivityLogger logger, AppDbContext db)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
        _db = db;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public void OnGet(string? error = null)
    {
        if (error == "ratelimit")
            ErrorMessage = "Too many login attempts. Please wait a minute and try again.";
    }

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

        try
        {
            // ?? Step 1: Check if the email exists at all ?????????????????????
            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                ErrorMessage = "No account found with that email address. Please check and try again.";
                return Page();
            }

            // ?? Step 2: Check if the account is active ???????????????????????
            if (!user.IsActive)
            {
                ErrorMessage = "Your account has been deactivated. Please contact your administrator.";
                return Page();
            }

            // ?? Step 3: Attempt sign-in ??????????????????????????????????????
            var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, isPersistent: true, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                if (user.TenantId.HasValue)
                {
                    await _logger.LogAsync(user.TenantId.Value, user.Id, "Login", "Auth",
                        $"{user.FullName} ({user.Role}) logged in",
                        ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                        userAgent: Request.Headers["User-Agent"].ToString());

                    // Auto Punch-In on login
                    var today = DateTime.UtcNow.Date;
                    var openEntry = await _db.AttendanceLogs
                        .Where(a => a.AgentId == user.Id
                                 && a.PunchIn >= today
                                 && a.PunchIn < today.AddDays(1)
                                 && a.PunchOut == null)
                        .FirstOrDefaultAsync();

                    if (openEntry == null)
                    {
                        _db.AttendanceLogs.Add(new AttendanceLog
                        {
                            TenantId      = user.TenantId.Value,
                            AgentId       = user.Id,
                            PunchIn       = DateTime.UtcNow,
                            PunchedInById = user.Id,
                            IsManualEntry = false,
                            Notes         = "Auto punch-in on login"
                        });
                        await _db.SaveChangesAsync();
                    }
                }

                if (user.Role == "superadmin")
                    return RedirectToPage("/SuperAdmin/Tenants");

                return LocalRedirect(returnUrl ?? "/Dashboard");
            }

            if (result.IsLockedOut)
            {
                ErrorMessage = "Account temporarily locked due to too many failed attempts. Please try again in 5 minutes.";
                return Page();
            }

            if (result.IsNotAllowed)
            {
                ErrorMessage = "Sign-in is not allowed for this account. Please contact your administrator.";
                return Page();
            }

            if (result.RequiresTwoFactor)
            {
                ErrorMessage = "Two-factor authentication is required. Please contact your administrator.";
                return Page();
            }

            // ?? Step 4: Password was wrong ???????????????????????????????????
            // Check remaining attempts and warn the user
            var failCount  = await _userManager.GetAccessFailedCountAsync(user);
            var maxAttempts = 5; // matches Lockout.MaxFailedAccessAttempts in Program.cs
            var remaining  = maxAttempts - failCount;

            ErrorMessage = remaining > 1
                ? $"Incorrect password. {remaining} attempt{(remaining == 1 ? "" : "s")} remaining before account lockout."
                : remaining == 1
                    ? "Incorrect password. 1 attempt remaining — your account will be locked after the next failed attempt."
                    : "Incorrect password. Please try again.";
        }
        catch (Exception)
        {
            ErrorMessage = "An unexpected error occurred while signing in. Please try again.";
        }

        return Page();
    }
}
