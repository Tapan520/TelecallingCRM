using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;

namespace TelecallingCRM.Services;

/// <summary>
/// Hangfire background job that runs AI summarization and sentiment analysis
/// after a call ends, so the agent's UI is never blocked waiting for AI.
/// </summary>
public interface ICallAiProcessor
{
    Task ProcessAsync(Guid callId, Guid tenantId);
}

public class CallAiProcessor : ICallAiProcessor
{
    private readonly AppDbContext _db;
    private readonly IOpenRouterService _ai;
    private readonly ILogger<CallAiProcessor> _logger;

    public CallAiProcessor(AppDbContext db, IOpenRouterService ai, ILogger<CallAiProcessor> logger)
    {
        _db = db;
        _ai = ai;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid callId, Guid tenantId)
    {
        var call = await _db.Calls.FindAsync(callId);
        if (call == null || string.IsNullOrWhiteSpace(call.TranscriptText)) return;

        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null) return;

        try
        {
            call.AiSummary = await _ai.SummarizeCallAsync(call.TranscriptText, tenant);
            call.AiSentiment = await _ai.AnalyzeSentimentAsync(call.TranscriptText, tenant);

            // Extract AI insight (competitor mention, objection type, etc.)
            var insightPrompt = "In one short sentence, identify if this call transcript mentions a competitor, " +
                                "a strong objection, or a high-interest signal. If none, reply 'none'.";
            var insight = await _ai.ChatAsync(insightPrompt, call.TranscriptText, tenant);
            if (!string.IsNullOrWhiteSpace(insight) &&
                !insight.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                call.AiInsight = insight;

                // Update lead AI insight as well
                var lead = await _db.Leads.FindAsync(call.LeadId);
                if (lead != null) lead.AiInsight = insight;
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("AI processing complete for call {CallId}", callId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI processing failed for call {CallId}", callId);
        }
    }
}
