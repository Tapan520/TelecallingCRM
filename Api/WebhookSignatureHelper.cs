using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;

namespace TelecallingCRM.Api;

/// <summary>
/// Verifies HMAC-SHA256 signatures on inbound webhooks.
/// Supports the X-Hub-Signature-256 header used by Meta/WhatsApp Business API
/// and X-Twilio-Signature for Twilio callbacks.
/// </summary>
public static class WebhookSignatureHelper
{
    /// <summary>
    /// Tries to verify the webhook signature against all active webhook secrets for the platform.
    /// Returns true if at least one secret matches or no secrets are configured (open mode).
    /// </summary>
    public static async Task<bool> VerifyAsync(HttpContext ctx, AppDbContext db)
    {
        ctx.Request.EnableBuffering();
        using var ms = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(ms);
        ms.Position = 0;
        ctx.Request.Body.Position = 0;
        var body = Encoding.UTF8.GetString(ms.ToArray());

        // Check X-Hub-Signature-256 (Meta / WhatsApp WABA)
        var hubSig = ctx.Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        // Check X-Twilio-Signature (Twilio)
        var twilioSig = ctx.Request.Headers["X-Twilio-Signature"].FirstOrDefault();

        if (string.IsNullOrEmpty(hubSig) && string.IsNullOrEmpty(twilioSig))
            return true; // No signature sent — accept in dev; lock down in production via config

        // Load all active webhook secrets across all tenants
        var secrets = await db.WebhookConfigs
            .Where(w => w.IsActive && w.Secret != null)
            .Select(w => w.Secret!)
            .ToListAsync();

        if (!secrets.Any()) return true; // no webhooks configured yet — accept

        foreach (var secret in secrets)
        {
            if (!string.IsNullOrEmpty(hubSig))
            {
                var expected = "sha256=" + ComputeHmacSha256(body, secret);
                if (hubSig.Equals(expected, StringComparison.OrdinalIgnoreCase)) return true;
            }
            if (!string.IsNullOrEmpty(twilioSig))
            {
                var url = $"{ctx.Request.Scheme}://{ctx.Request.Host}{ctx.Request.Path}";
                var expected = ComputeHmacSha1(url + body, secret);
                if (twilioSig.Equals(expected, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }

        return false;
    }

    private static string ComputeHmacSha256(string data, string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(dataBytes)).ToLower();
    }

    private static string ComputeHmacSha1(string data, string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA1(keyBytes);
        return Convert.ToBase64String(hmac.ComputeHash(dataBytes));
    }
}
