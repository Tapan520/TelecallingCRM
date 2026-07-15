using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Pages;

public class ResetPasswordModel : PageModel
{
    private readonly UserManager<AppUser> _userManager;

    public ResetPasswordModel(UserManager<AppUser> userManager) => _userManager = userManager;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool ResetDone { get; set; }
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required] public string Token { get; set; } = string.Empty;
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required, MinLength(8)] public string NewPassword { get; set; } = string.Empty;
        [Compare(nameof(NewPassword))] public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void OnGet(string token, string email)
    {
        Input = new InputModel { Token = Uri.UnescapeDataString(token), Email = email };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await _userManager.FindByEmailAsync(Input.Email);
        if (user == null) { ResetDone = true; return Page(); }

        var result = await _userManager.ResetPasswordAsync(user, Input.Token, Input.NewPassword);
        if (result.Succeeded) { ResetDone = true; return Page(); }

        ErrorMessage = string.Join(". ", result.Errors.Select(e => e.Description));
        return Page();
    }
}
