using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Services;

/// <summary>
/// Handles round-robin lead assignment across available (online + in-shift) agents.
/// </summary>
public interface ILeadAssignmentService
{
    Task<Guid?> GetNextAgentAsync(Guid tenantId, Guid? campaignId, CancellationToken ct = default);
    Task AssignRoundRobinAsync(Guid leadId, Guid tenantId, Guid? campaignId, CancellationToken ct = default);
}

public class LeadAssignmentService : ILeadAssignmentService
{
    private readonly AppDbContext _db;
    private readonly ILogger<LeadAssignmentService> _logger;

    public LeadAssignmentService(AppDbContext db, ILogger<LeadAssignmentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Guid?> GetNextAgentAsync(Guid tenantId, Guid? campaignId, CancellationToken ct = default)
    {
        // Fetch (or create) the round-robin state for this tenant/campaign
        var state = await _db.RoundRobinStates
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.CampaignId == campaignId, ct);

        if (state == null)
        {
            // Build initial agent queue from active agents with a shift configured
            var agents = await GetAvailableAgentsAsync(tenantId, ct);
            if (!agents.Any()) return null;

            state = new RoundRobinState
            {
                TenantId = tenantId,
                CampaignId = campaignId,
                AgentQueueJson = JsonSerializer.Serialize(agents),
                NextIndex = 0
            };
            _db.RoundRobinStates.Add(state);
            await _db.SaveChangesAsync(ct);
        }

        var queue = JsonSerializer.Deserialize<List<Guid>>(state.AgentQueueJson) ?? new List<Guid>();
        if (!queue.Any()) return null;

        // Refresh queue periodically (every full rotation) to pick up new / removed agents
        if (state.NextIndex >= queue.Count)
        {
            queue = await GetAvailableAgentsAsync(tenantId, ct);
            state.AgentQueueJson = JsonSerializer.Serialize(queue);
            state.NextIndex = 0;
        }

        if (!queue.Any()) return null;

        var agentId = queue[state.NextIndex % queue.Count];
        state.NextIndex = (state.NextIndex + 1) % queue.Count;
        state.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return agentId;
    }

    public async Task AssignRoundRobinAsync(Guid leadId, Guid tenantId, Guid? campaignId, CancellationToken ct = default)
    {
        var lead = await _db.Leads.FindAsync(new object[] { leadId }, ct);
        if (lead == null) return;

        var agentId = await GetNextAgentAsync(tenantId, campaignId, ct);
        if (agentId == null) return;

        lead.AssignedToId = agentId;
        lead.UpdatedAt = DateTime.UtcNow;
        _db.ActivityLogs.Add(new ActivityLog
        {
            TenantId = tenantId,
            LeadId = leadId,
            UserId = agentId.Value,
            Type = ActivityType.LeadAssigned,
            Summary = $"Auto-assigned via round-robin to agent {agentId}"
        });
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Round-robin: lead {LeadId} assigned to agent {AgentId}", leadId, agentId);
    }

    private async Task<List<Guid>> GetAvailableAgentsAsync(Guid tenantId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var dayBit = (int)Math.Pow(2, ((int)now.DayOfWeek + 6) % 7); // Mon=1,Tue=2,...Sun=64

        // Agents with an active shift covering the current UTC time
        var agentsWithShift = await _db.AgentShifts
            .Where(s => s.TenantId == tenantId
                     && s.IsActive
                     && (s.WorkDays & dayBit) != 0
                     && s.ShiftStartUtc <= now.TimeOfDay
                     && s.ShiftEndUtc >= now.TimeOfDay)
            .Select(s => s.AgentId)
            .Distinct()
            .ToListAsync(ct);

        // Also include agents who manually marked themselves online (even without a shift)
        var onlineAgents = await _db.AgentPresences
            .Where(p => p.TenantId == tenantId && p.IsOnline)
            .GroupBy(p => p.AgentId)
            .Select(g => g.OrderByDescending(x => x.ChangedAt).First().AgentId)
            .ToListAsync(ct);

        // Combine and filter to active users in this tenant
        var allEligible = agentsWithShift.Union(onlineAgents).Distinct().ToList();

        var activeAgents = await _db.Users
            .Where(u => u.TenantId == tenantId
                     && u.IsActive
                     && u.Role == "agent"
                     && allEligible.Contains(u.Id))
            .OrderBy(u => u.FullName)
            .Select(u => u.Id)
            .ToListAsync(ct);

        return activeAgents;
    }
}
