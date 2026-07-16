using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace TelecallingCRM.Services;

/// <summary>
/// Lightweight JSON-file-based localisation service.
/// Falls back to English when a key is not found in the requested locale.
/// Supported cultures: en (English), hi (Hindi).
/// </summary>
public interface ILocalizationService
{
    string Get(string key, string? culture = null);
    Dictionary<string, object> GetAll(string? culture = null);
}

public class LocalizationService : ILocalizationService
{
    private readonly IWebHostEnvironment _env;
    private readonly IMemoryCache _cache;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LocalizationService(IWebHostEnvironment env, IMemoryCache cache, IHttpContextAccessor httpContextAccessor)
    {
        _env = env;
        _cache = cache;
        _httpContextAccessor = httpContextAccessor;
    }

    public string Get(string key, string? culture = null)
    {
        culture ??= ResolveCulture();
        var dict = LoadFlat(culture);

        if (dict.TryGetValue(key, out var val)) return val;

        // Fallback to English
        if (culture != "en")
        {
            var enDict = LoadFlat("en");
            if (enDict.TryGetValue(key, out var enVal)) return enVal;
        }

        return key; // Last resort: return the key itself
    }

    public Dictionary<string, object> GetAll(string? culture = null)
    {
        culture ??= ResolveCulture();
        var cacheKey = $"locale_{culture}";
        if (_cache.TryGetValue(cacheKey, out Dictionary<string, object>? cached) && cached != null)
            return cached;

        var path = Path.Combine(_env.ContentRootPath, "Resources", $"{culture}.json");
        if (!File.Exists(path))
            path = Path.Combine(_env.ContentRootPath, "Resources", "en.json");

        var json = File.ReadAllText(path);
        var doc = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new();
        var result = new Dictionary<string, object>();
        foreach (var (k, v) in doc) result[k] = v;

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(30));
        return result;
    }

    private Dictionary<string, string> LoadFlat(string culture)
    {
        var cacheKey = $"locale_flat_{culture}";
        if (_cache.TryGetValue(cacheKey, out Dictionary<string, string>? cached) && cached != null)
            return cached;

        var path = Path.Combine(_env.ContentRootPath, "Resources", $"{culture}.json");
        if (!File.Exists(path))
            path = Path.Combine(_env.ContentRootPath, "Resources", "en.json");

        var json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json);
        var flat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Flatten(doc.RootElement, "", flat);
        _cache.Set(cacheKey, flat, TimeSpan.FromMinutes(30));
        return flat;
    }

    private static void Flatten(JsonElement element, string prefix, Dictionary<string, string> result)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var prop in element.EnumerateObject())
                Flatten(prop.Value, string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}", result);
        else if (element.ValueKind == JsonValueKind.String)
            result[prefix] = element.GetString() ?? prefix;
    }

    private string ResolveCulture()
    {
        var ctx = _httpContextAccessor.HttpContext;
        // 1. Query string ?lang=hi
        if (ctx?.Request.Query.TryGetValue("lang", out var q) == true && !string.IsNullOrEmpty(q))
            return Sanitize(q!);
        // 2. Cookie
        if (ctx?.Request.Cookies.TryGetValue("lang", out var c) == true && !string.IsNullOrEmpty(c))
            return Sanitize(c);
        // 3. Accept-Language header
        var acceptLang = ctx?.Request.Headers["Accept-Language"].FirstOrDefault();
        if (!string.IsNullOrEmpty(acceptLang))
        {
            var lang = acceptLang.Split(',').First().Trim().Split('-').First().ToLowerInvariant();
            return Sanitize(lang);
        }
        return "en";
    }

    private static string Sanitize(string lang)
    {
        var supported = new[] { "en", "hi" };
        return supported.Contains(lang.ToLowerInvariant()) ? lang.ToLowerInvariant() : "en";
    }
}
