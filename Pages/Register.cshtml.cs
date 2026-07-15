using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Pages;

public class RegisterModel : PageModel
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly AppDbContext _db;

    public RegisterModel(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, AppDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required] public string CompanyName { get; set; } = string.Empty;
        [Required, RegularExpression(@"^[a-z0-9\-]+$", ErrorMessage = "Lowercase letters, numbers and hyphens only.")]
        public string TenantSlug { get; set; } = string.Empty;
        [Required] public string FullName { get; set; } = string.Empty;
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required, MinLength(8)] public string Password { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        if (await _db.Tenants.AnyAsync(t => t.Slug == Input.TenantSlug))
        {
            ErrorMessage = "That tenant slug is already taken. Choose another.";
            return Page();
        }

        var tenant = new Tenant { Name = Input.CompanyName, Slug = Input.TenantSlug.ToLower() };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        var user = new AppUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            FullName = Input.FullName,
            TenantId = tenant.Id,
            Role = "admin"
        };

        var result = await _userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            ErrorMessage = string.Join(", ", result.Errors.Select(e => e.Description));
            return Page();
        }

        await _signInManager.SignInAsync(user, isPersistent: true);
        return RedirectToPage("/Dashboard/Index");
    }
}
