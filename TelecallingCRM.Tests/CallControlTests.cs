using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Tests;

/// <summary>Tests for #1 – Call Controls module.</summary>
public class CallControlTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _agentId = Guid.NewGuid();
    private readonly Guid _callId = Guid.NewGuid();

    public CallControlTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(opts);

        // Seed minimum data
        _db.Set<Tenant>().Add(new Tenant { Id = _tenantId, Name = "T1", Slug = "t1" });

        _db.Set<AppUser>().Add(new AppUser
        {
            Id = _agentId,
            TenantId = _tenantId,
            FullName = "Agent One",
            UserName = "agent1",
            NormalizedUserName = "AGENT1",
            Email = "a@t.com",
            NormalizedEmail = "A@T.COM",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        _db.Set<Lead>().Add(new Lead
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Lead X",
            Phone = "9999999999"
        });

        var lead = _db.Set<Lead>().Local.First();

        _db.Set<Call>().Add(new Call
        {
            Id = _callId,
            TenantId = _tenantId,
            LeadId = lead.Id,
            AgentId = _agentId
        });

        _db.SaveChanges();
    }

    [Fact]
    public async Task CanRecordMuteEvent()
    {
        var evt = new CallControlEvent
        {
            CallId = _callId,
            AgentId = _agentId,
            Action = CallControlAction.Mute,
            OccurredAt = DateTime.UtcNow
        };
        _db.CallControlEvents.Add(evt);
        await _db.SaveChangesAsync();

        var saved = await _db.CallControlEvents
            .SingleAsync(e => e.CallId == _callId && e.Action == CallControlAction.Mute);
        Assert.Equal(_callId, saved.CallId);
        Assert.Equal(_agentId, saved.AgentId);
    }

    [Fact]
    public async Task CanRecordTransferEventWithTargetParty()
    {
        var evt = new CallControlEvent
        {
            CallId = _callId,
            AgentId = _agentId,
            Action = CallControlAction.Transfer,
            TargetParty = "+911234567890",
            OccurredAt = DateTime.UtcNow
        };
        _db.CallControlEvents.Add(evt);
        await _db.SaveChangesAsync();

        var saved = await _db.CallControlEvents
            .SingleAsync(e => e.Action == CallControlAction.Transfer);
        Assert.Equal("+911234567890", saved.TargetParty);
    }

    [Fact]
    public async Task CanRecordMultipleControlEventsForSameCall()
    {
        var actions = new[]
        {
            CallControlAction.Mute,
            CallControlAction.Unmute,
            CallControlAction.Hold,
            CallControlAction.Resume
        };

        foreach (var action in actions)
        {
            _db.CallControlEvents.Add(new CallControlEvent
            {
                CallId = _callId,
                AgentId = _agentId,
                Action = action,
                OccurredAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();

        var count = await _db.CallControlEvents
            .CountAsync(e => e.CallId == _callId);
        Assert.Equal(4, count);
    }

    [Fact]
    public async Task ConferenceEventIsPersistedCorrectly()
    {
        var evt = new CallControlEvent
        {
            CallId = _callId,
            AgentId = _agentId,
            Action = CallControlAction.Conference,
            TargetParty = "supervisor@crm",
            OccurredAt = DateTime.UtcNow
        };
        _db.CallControlEvents.Add(evt);
        await _db.SaveChangesAsync();

        var saved = await _db.CallControlEvents
            .SingleAsync(e => e.Action == CallControlAction.Conference);
        Assert.Equal("supervisor@crm", saved.TargetParty);
        Assert.Equal(CallControlAction.Conference, saved.Action);
    }

    [Fact]
    public async Task CallControlEvent_HasNavigationToCall()
    {
        _db.CallControlEvents.Add(new CallControlEvent
        {
            CallId = _callId,
            AgentId = _agentId,
            Action = CallControlAction.Hold,
            OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var evt = await _db.CallControlEvents
            .Include(e => e.Call)
            .FirstAsync(e => e.Action == CallControlAction.Hold);

        Assert.NotNull(evt.Call);
        Assert.Equal(_callId, evt.Call.Id);
    }

    public void Dispose() => _db.Dispose();
}
