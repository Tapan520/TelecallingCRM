using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class LocalizationEndpoints
{
    public static void MapLocalizationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/i18n").WithTags("Localization");

        // GET /api/i18n/{culture}  — returns full locale JSON for the given culture
        group.MapGet("/{culture}", (string culture, ILocalizationService loc) =>
        {
            var strings = loc.GetAll(culture);
            return Results.Ok(strings);
        });

        // GET /api/i18n/supported  — list of supported cultures
        group.MapGet("/supported", () =>
            Results.Ok(new[] {
                new { code = "en", label = "English" },
                new { code = "hi", label = "?????? (Hindi)" }
            })
        );
    }
}
