using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Services;

public interface IOpenRouterService
{
    Task<string> ChatAsync(string userMessage, string? systemPrompt, Tenant tenant, CancellationToken ct = default);
    Task<string> SummarizeCallAsync(string transcript, Tenant tenant, CancellationToken ct = default);
    Task<string> AnalyzeSentimentAsync(string transcript, Tenant tenant, CancellationToken ct = default);
    Task<float[]> GetEmbeddingAsync(string text, Tenant tenant, CancellationToken ct = default);
}

public class OpenRouterService : IOpenRouterService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;

    public OpenRouterService(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _httpFactory = httpFactory;
        _config = config;
    }

    private string GetApiKey(Tenant tenant) =>
        tenant.OpenRouterApiKey ?? _config["OpenRouter:ApiKey"] ?? string.Empty;

    private string GetModel(Tenant tenant) =>
        tenant.PreferredModel ?? _config["OpenRouter:DefaultModel"] ?? "openai/gpt-4o-mini";

    public async Task<string> ChatAsync(string userMessage, string? systemPrompt, Tenant tenant, CancellationToken ct = default)
    {
        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(systemPrompt))
            messages.Add(new { role = "system", content = systemPrompt });
        messages.Add(new { role = "user", content = userMessage });

        return await SendChatAsync(messages, tenant, ct);
    }

    public async Task<string> SummarizeCallAsync(string transcript, Tenant tenant, CancellationToken ct = default)
    {
        var messages = new List<object>
        {
            new { role = "system", content = "You are a CRM assistant. Summarize the following telecalling conversation in 2-3 concise sentences, highlighting key outcomes and next steps." },
            new { role = "user", content = transcript }
        };
        return await SendChatAsync(messages, tenant, ct);
    }

    public async Task<string> AnalyzeSentimentAsync(string transcript, Tenant tenant, CancellationToken ct = default)
    {
        var messages = new List<object>
        {
            new { role = "system", content = "Analyze the sentiment of the following sales call transcript. Reply with exactly one word: positive, neutral, or negative." },
            new { role = "user", content = transcript }
        };
        return await SendChatAsync(messages, tenant, ct);
    }

    public async Task<float[]> GetEmbeddingAsync(string text, Tenant tenant, CancellationToken ct = default)
    {
        var apiKey = GetApiKey(tenant);
        var client = _httpFactory.CreateClient("openrouter");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var payload = JsonSerializer.Serialize(new
        {
            model = "openai/text-embedding-ada-002",
            input = text
        });

        var response = await client.PostAsync(
            "https://openrouter.ai/api/v1/embeddings",
            new StringContent(payload, Encoding.UTF8, "application/json"), ct);

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var embeddingArray = doc.RootElement
            .GetProperty("data")[0]
            .GetProperty("embedding");

        return embeddingArray.EnumerateArray().Select(e => e.GetSingle()).ToArray();
    }

    private async Task<string> SendChatAsync(IEnumerable<object> messages, Tenant tenant, CancellationToken ct)
    {
        var apiKey = GetApiKey(tenant);
        if (string.IsNullOrWhiteSpace(apiKey))
            return "[AI not configured – add OpenRouter API key in tenant settings]";

        var client = _httpFactory.CreateClient("openrouter");

        var payload = JsonSerializer.Serialize(new { model = GetModel(tenant), messages });

        // Use per-request HttpRequestMessage to avoid mutating shared DefaultRequestHeaders
        var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Add("HTTP-Referer", "https://telcallingcrm.app");
        request.Headers.Add("X-Title", "TelecallingCRM");

        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }
}
