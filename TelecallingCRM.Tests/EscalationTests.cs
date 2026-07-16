using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Tests;

/// <summary>Tests for #2 – Escalation module (rules + instances).</summary>
public class EscalationTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _agentId = Guid.NewGuid();
    private readonly Guid _leadId = Guid.NewGuid();

    public EscalationTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(opts);

        _db.Set<Tenant>().Add(new Tenant { Id = _tenantId, Name = "T2", Slug = "t2" });

        _db.Set<AppUser>().AddRange(
            new AppUser { Id = _agentId, TenantId = _tenantId, FullName = "Agent", UserName = "ag", NormalizedUserName = "AG", Email = "ag@t.com", NormalizedEmail = "AG@T.COM", SecurityStamp = Guid.NewGuid().ToString() },
            new AppUser { Id = _managerId, TenantId = _tenantId, FullName = "Manager", UserName = "mgr", NormalizedUserName = "MGR", Email = "mgr@t.com", NormalizedEmail = "MGR@T.COM", SecurityStamp = Guid.NewGuid().ToString() }
        );

        _db.Set<Lead>().Add(new Lead { Id = _leadId, TenantId = _tenantId, Name = "Lead Y", Phone = "8888888888" });

        _db.SaveChanges();
    }

    // ?? Escalation Rule tests ??????????????????????????????????????????????????

    [Fact]
    public async Task CanCreateEscalationRule()
    {
        var rule = new EscalationRule
        {
            TenantId = _tenantId,
            Name = "Missed Follow-Up Rule",
            Trigger = EscalationTrigger.MissedFollowUp,
            ThresholdValue = 3,
            EscalateToId = _managerId,
            IsActive = true
        };
        _db.EscalationRules.Add(rule);
        await _db.SaveChangesAsync();

        var saved = await _db.EscalationRules.SingleAsync(r => r.Name == "Missed Follow-Up Rule");
        Assert.Equal(EscalationTrigger.MissedFollowUp, saved.Trigger);
        Assert.Equal(3, saved.ThresholdValue);
        Assert.True(saved.IsActive);
    }

    [Fact]
    public async Task CanToggleEscalationRuleActive()
    {
        var rule = new EscalationRule
        {
            TenantId = _tenantId, Name = "Hot-Lead Rule",
            Trigger = EscalationTrigger.HotLeadIgnored,
            ThresholdValue = 1, EscalateToId = _managerId, IsActive = true
        };
        _db.EscalationRules.Add(rule);
        await _db.SaveChangesAsync();

        rule.IsActive = false;
        await _db.SaveChangesAsync();

        var saved = await _db.EscalationRules.SingleAsync(r => r.Id == rule.Id);
        Assert.False(saved.IsActive);
    }

    [Fact]
    public async Task CanDeleteEscalationRule()
    {
        var rule = new EscalationRule
        {
            TenantId = _tenantId, Name = "Temp Rule",
            Trigger = EscalationTrigger.OverdueTask,
            ThresholdValue = 2, EscalateToId = _managerId
        };
        _db.EscalationRules.Add(rule);
        await _db.SaveChangesAsync();

        _db.EscalationRules.Remove(rule);
        await _db.SaveChangesAsync();

        Assert.Equal(0, await _db.EscalationRules.CountAsync(r => r.Id == rule.Id));
    }

    // ?? Escalation Instance tests ??????????????????????????????????????????????

    [Fact]
    public async Task CanRaiseEscalation()
    {
        var esc = new Escalation
        {
            TenantId = _tenantId,
            LeadId = _leadId,
            AssignedAgentId = _agentId,
            EscalatedToId = _managerId,
            Reason = "Agent not responding",
            Status = EscalationStatus.Pending
        };
        _db.Escalations.Add(esc);
        await _db.SaveChangesAsync();

        var saved = await _db.Escalations.SingleAsync(e => e.LeadId == _leadId);
        Assert.Equal(EscalationStatus.Pending, saved.Status);
        Assert.Equal("Agent not responding", saved.Reason);
    }

    [Fact]
    public async Task CanAcknowledgeEscalation()
    {
        var esc = new Escalation
        {
            TenantId = _tenantId, LeadId = _leadId,
            AssignedAgentId = _agentId, EscalatedToId = _managerId,
            Reason = "Missed follow-up", Status = EscalationStatus.Pending
        };
        _db.Escalations.Add(esc);
        await _db.SaveChangesAsync();

        esc.Status = EscalationStatus.Acknowledged;
        esc.AcknowledgedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var saved = await _db.Escalations.SingleAsync(e => e.Id == esc.Id);
        Assert.Equal(EscalationStatus.Acknowledged, saved.Status);
        Assert.NotNull(saved.AcknowledgedAt);
    }

    [Fact]
    public async Task CanResolveEscalationWithNote()
    {
        var esc = new Escalation
        {
            TenantId = _tenantId, LeadId = _leadId,
            AssignedAgentId = _agentId, EscalatedToId = _managerId,
            Reason = "Overdue", Status = EscalationStatus.Pending
        };
        _db.Escalations.Add(esc);
        await _db.SaveChangesAsync();

        esc.Status = EscalationStatus.Resolved;
        esc.ResolvedAt = DateTime.UtcNow;
        esc.ResolutionNote = "Called and resolved";
        await _db.SaveChangesAsync();

        var saved = await _db.Escalations.SingleAsync(e => e.Id == esc.Id);
        Assert.Equal(EscalationStatus.Resolved, saved.Status);
        Assert.Equal("Called and resolved", saved.ResolutionNote);
        Assert.NotNull(saved.ResolvedAt);
    }

    [Fact]
    public async Task CanDismissEscalation()
    {
        var esc = new Escalation
        {
            TenantId = _tenantId, LeadId = _leadId,
            AssignedAgentId = _agentId, EscalatedToId = _managerId,
            Reason = "Duplicate", Status = EscalationStatus.Pending
        };
        _db.Escalations.Add(esc);
        await _db.SaveChangesAsync();

        esc.Status = EscalationStatus.Dismissed;
        await _db.SaveChangesAsync();

        var saved = await _db.Escalations.SingleAsync(e => e.Id == esc.Id);
        Assert.Equal(EscalationStatus.Dismissed, saved.Status);
    }

    [Fact]
    public async Task EscalationLinksToRuleWhenProvided()
    {
        var rule = new EscalationRule
        {
            TenantId = _tenantId, Name = "Auto Rule",
            Trigger = EscalationTrigger.NoContactDays,
            ThresholdValue = 5, EscalateToId = _managerId
        };
        _db.EscalationRules.Add(rule);
        await _db.SaveChangesAsync();

        var esc = new Escalation
        {
            TenantId = _tenantId, LeadId = _leadId,
            AssignedAgentId = _agentId, EscalatedToId = _managerId,
            Reason = "Auto", RuleId = rule.Id
        };
        _db.Escalations.Add(esc);
        await _db.SaveChangesAsync();

        var saved = await _db.Escalations
            .Include(e => e.Rule)
            .SingleAsync(e => e.Id == esc.Id);
        Assert.NotNull(saved.Rule);
        Assert.Equal("Auto Rule", saved.Rule!.Name);
    }

    public void Dispose() => _db.Dispose();
}
