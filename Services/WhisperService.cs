using System.Net.Http.Headers;
using System.Text.Json;

namespace TelecallingCRM.Services;

public interface IWhisperService
{
    /// <summary>Transcribes the uploaded audio file using OpenAI-compatible Whisper endpoint.</summary>
    Task<string> TranscribeAsync(IFormFile audioFile, string apiKey, CancellationToken ct = default);
}

public class WhisperService : IWhisperService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;

    public WhisperService(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _httpFactory = httpFactory;
        _config = config;
    }

    public async Task<string> TranscribeAsync(IFormFile audioFile, string apiKey, CancellationToken ct = default)
    {
        var key = string.IsNullOrWhiteSpace(apiKey)
            ? (_config["OpenRouter:ApiKey"] ?? string.Empty)
            : apiKey;

        if (string.IsNullOrWhiteSpace(key))
            return "[Whisper not configured – add API key in tenant settings]";

        var client = _httpFactory.CreateClient("whisper");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);

        using var form = new MultipartFormDataContent();
        await using var stream = audioFile.OpenReadStream();
        var audioContent = new StreamContent(stream);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue(audioFile.ContentType);
        form.Add(audioContent, "file", audioFile.FileName);
        form.Add(new StringContent("whisper-1"), "model");

        var whisperEndpoint = _config["Whisper:Endpoint"] ?? "https://api.openai.com/v1/audio/transcriptions";
        var response = await client.PostAsync(whisperEndpoint, form, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("text").GetString() ?? string.Empty;
    }
}
