using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Pages;

public class LogoutModel : PageModel
{
    private readonly SignInManager<AppUser> _signInManager;
    private readonly AppDbContext _db;
    private readonly IUserActivityLogger _activityLogger;

    public LogoutModel(SignInManager<AppUser> signInManager, AppDbContext db, IUserActivityLogger activityLogger)
    {
        _signInManager = signInManager;
        _db = db;
        _activityLogger = activityLogger;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            var today = DateTime.UtcNow.Date;
            var openEntry = await _db.AttendanceLogs
                .Where(a => a.AgentId == userId
                         && a.PunchIn >= today
                         && a.PunchIn < today.AddDays(1)
                         && a.PunchOut == null)
                .OrderByDescending(a => a.PunchIn)
                .FirstOrDefaultAsync();

            if (openEntry != null)
            {
                var now = DateTime.UtcNow;
                openEntry.PunchOut       = now;
                openEntry.WorkMinutes    = (int)(now - openEntry.PunchIn).TotalMinutes;
                openEntry.PunchedOutById = userId;
                openEntry.UpdatedAt      = now;
                openEntry.Notes          = string.IsNullOrEmpty(openEntry.Notes)
                    ? "Auto punch-out on logout"
                    : openEntry.Notes + " [Auto punch-out on logout]";
                openEntry.Status         = openEntry.WorkMinutes >= 240 ? AttendanceStatus.Present
                                         : openEntry.WorkMinutes >= 60  ? AttendanceStatus.HalfDay
                                         : AttendanceStatus.Present;
                await _db.SaveChangesAsync();
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user?.TenantId != null)
                await _activityLogger.LogAsync(user.TenantId.Value, userId, "Logout", "Auth",
                    $"{user.FullName} ({user.Role}) logged out",
                    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
        }

        await _signInManager.SignOutAsync();
        return RedirectToPage("/Login");
    }
}


