using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class NotificationPreferenceEndpoints
{
    public static void MapNotificationPreferenceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notification-preferences")
            .WithTags("NotificationPreferences")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapGet("/", async (AppDbContext db, HttpContext http) =>
        {
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var prefs = await db.NotificationPreferences
                .Where(p => p.UserId == userId)
                .ToListAsync();

            // Return all notification types, merging with saved preferences
            var allTypes = Enum.GetNames(typeof(NotificationType));
            var result = allTypes.Select(t =>
            {
                var pref = prefs.FirstOrDefault(p => p.NotificationType == t);
                return new {
                    NotificationType = t,
                    InApp = pref?.InApp ?? true,
                    Email = pref?.Email ?? false,
                    QuietHoursEnabled = pref?.QuietHoursEnabled ?? false,
                    QuietHoursStart = pref?.QuietHoursStart ?? 22,
                    QuietHoursEnd = pref?.QuietHoursEnd ?? 8
                };
            });
            return Results.Ok(result);
        });

        group.MapPut("/", async ([FromBody] List<NotifPrefDto> dto,
            AppDbContext db, HttpContext http) =>
        {
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            foreach (var item in dto)
            {
                var pref = await db.NotificationPreferences
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.NotificationType == item.NotificationType);
                if (pref == null)
                {
                    pref = new NotificationPreference { UserId = userId, NotificationType = item.NotificationType };
                    db.NotificationPreferences.Add(pref);
                }
                pref.InApp = item.InApp;
                pref.Email = item.Email;
                pref.QuietHoursEnabled = item.QuietHoursEnabled;
                pref.QuietHoursStart = item.QuietHoursStart;
                pref.QuietHoursEnd = item.QuietHoursEnd;
            }
            await db.SaveChangesAsync();
            return Results.Ok();
        });
    }
}

public record NotifPrefDto(string NotificationType, bool InApp, bool Email,
    bool QuietHoursEnabled, int QuietHoursStart, int QuietHoursEnd);
