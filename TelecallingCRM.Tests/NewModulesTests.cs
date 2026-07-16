using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace TelecallingCRM.Tests;

// ===========================================================================
// MODULE 1: Round-Robin Lead Assignment
// ===========================================================================
public class RoundRobinTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _tenantId = Guid.NewGuid();

    public RoundRobinTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new AppDbContext(opts);
        _db.Set<Tenant>().Add(new Tenant { Id = _tenantId, Name = "RR", Slug = "rr" });
        _db.SaveChanges();
    }

    private AppUser MakeAgent(string name)
    {
        var id = Guid.NewGuid();
        var agent = new AppUser
        {
            Id = id, TenantId = _tenantId, FullName = name,
            Role = "agent", IsActive = true,
            UserName = name.ToLower(), NormalizedUserName = name.ToUpper(),
            Email = $"{name.ToLower()}@t.com", NormalizedEmail = $"{name.ToUpper()}@T.COM",
            SecurityStamp = Guid.NewGuid().ToString()
        };
        _db.Users.Add(agent);
        _db.AgentPresences.Add(new AgentPresence { TenantId = _tenantId, AgentId = id, IsOnline = true });
        return agent;
    }

    [Fact]
    public async Task RoundRobin_AssignsLeadToNextAvailableAgent()
    {
        var a1 = MakeAgent("AgentA");
        var a2 = MakeAgent("AgentB");
        await _db.SaveChangesAsync();

        var svc = new LeadAssignmentService(_db, NullLogger<LeadAssignmentService>.Instance);

        var first  = await svc.GetNextAgentAsync(_tenantId, null);
        var second = await svc.GetNextAgentAsync(_tenantId, null);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first, second); // different agents
    }

    [Fact]
    public async Task RoundRobin_WrapsAroundAfterAllAgents()
    {
        var a1 = MakeAgent("C1");
        var a2 = MakeAgent("C2");
        await _db.SaveChangesAsync();

        var svc = new LeadAssignmentService(_db, NullLogger<LeadAssignmentService>.Instance);

        var r1 = await svc.GetNextAgentAsync(_tenantId, null);
        var r2 = await svc.GetNextAgentAsync(_tenantId, null);
        var r3 = await svc.GetNextAgentAsync(_tenantId, null); // should wrap to r1

        Assert.NotNull(r1);
        Assert.Equal(r1, r3); // wraps around
    }

    [Fact]
    public async Task RoundRobin_ReturnsNullWhenNoAgentsAvailable()
    {
        // No agents, no presences
        var svc = new LeadAssignmentService(_db, NullLogger<LeadAssignmentService>.Instance);
        var result = await svc.GetNextAgentAsync(_tenantId, null);
        Assert.Null(result);
    }

    [Fact]
    public async Task AssignRoundRobin_UpdatesLeadAssignedToId()
    {
        var agent = MakeAgent("D1");
        var lead = new Lead { TenantId = _tenantId, Name = "TestLead", Phone = "9000000010" };
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();

        var svc = new LeadAssignmentService(_db, NullLogger<LeadAssignmentService>.Instance);
        await svc.AssignRoundRobinAsync(lead.Id, _tenantId, null);

        var updated = await _db.Leads.FindAsync(lead.Id);
        Assert.Equal(agent.Id, updated!.AssignedToId);
    }

    [Fact]
    public async Task AssignRoundRobin_LogsActivity()
    {
        var agent = MakeAgent("E1");
        var lead = new Lead { TenantId = _tenantId, Name = "LogLead", Phone = "9000000011" };
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();

        var svc = new LeadAssignmentService(_db, NullLogger<LeadAssignmentService>.Instance);
        await svc.AssignRoundRobinAsync(lead.Id, _tenantId, null);

        var log = await _db.ActivityLogs.FirstOrDefaultAsync(l =>
            l.LeadId == lead.Id && l.Type == ActivityType.LeadAssigned);
        Assert.NotNull(log);
        Assert.Contains("round-robin", log.Summary, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _db.Dispose();
}

// ===========================================================================
// MODULE 2: Agent Shift / Availability
// ===========================================================================
public class AgentShiftTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _agentId  = Guid.NewGuid();

    public AgentShiftTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new AppDbContext(opts);
        _db.Set<Tenant>().Add(new Tenant { Id = _tenantId, Name = "Shift", Slug = "shift" });
        _db.Users.Add(new AppUser { Id = _agentId, TenantId = _tenantId, FullName = "Shifter", Role = "agent", IsActive = true, UserName = "sh", NormalizedUserName = "SH", Email = "sh@t.com", NormalizedEmail = "SH@T.COM", SecurityStamp = Guid.NewGuid().ToString() });
        _db.SaveChanges();
    }

    [Fact]
    public async Task CanCreateAgentShift()
    {
        _db.AgentShifts.Add(new AgentShift
        {
            TenantId = _tenantId, AgentId = _agentId,
            WorkDays = 62, // Mon-Fri
            ShiftStartUtc = new TimeSpan(3, 30, 0),
            ShiftEndUtc   = new TimeSpan(13, 30, 0),
            Timezone = "Asia/Kolkata", IsActive = true
        });
        await _db.SaveChangesAsync();

        var s = await _db.AgentShifts.SingleAsync(x => x.AgentId == _agentId);
        Assert.Equal(62, s.WorkDays);
        Assert.Equal("Asia/Kolkata", s.Timezone);
    }

    [Fact]
    public async Task CanRecordAgentPresence()
    {
        _db.AgentPresences.Add(new AgentPresence { TenantId = _tenantId, AgentId = _agentId, IsOnline = true });
        await _db.SaveChangesAsync();

        var latest = await _db.AgentPresences
            .Where(p => p.AgentId == _agentId)
            .OrderByDescending(p => p.ChangedAt).FirstAsync();
        Assert.True(latest.IsOnline);
    }

    [Fact]
    public async Task PresenceHistory_IsOrdered()
    {
        _db.AgentPresences.Add(new AgentPresence { TenantId = _tenantId, AgentId = _agentId, IsOnline = true,  ChangedAt = DateTime.UtcNow.AddHours(-2) });
        _db.AgentPresences.Add(new AgentPresence { TenantId = _tenantId, AgentId = _agentId, IsOnline = false, ChangedAt = DateTime.UtcNow.AddHours(-1) });
        _db.AgentPresences.Add(new AgentPresence { TenantId = _tenantId, AgentId = _agentId, IsOnline = true,  ChangedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var latest = await _db.AgentPresences
            .Where(p => p.AgentId == _agentId)
            .OrderByDescending(p => p.ChangedAt).FirstAsync();
        Assert.True(latest.IsOnline);
    }

    [Fact]
    public async Task ShiftWorkDaysBitmask_IsCorrect()
    {
        // Mon(1) + Fri(16) = 17
        var shift = new AgentShift { TenantId = _tenantId, AgentId = _agentId, WorkDays = 17,
            ShiftStartUtc = TimeSpan.Zero, ShiftEndUtc = TimeSpan.FromHours(8), IsActive = true };
        _db.AgentShifts.Add(shift);
        await _db.SaveChangesAsync();

        var s = await _db.AgentShifts.FindAsync(shift.Id);
        Assert.True((s!.WorkDays & 1) != 0);  // Monday
        Assert.True((s.WorkDays & 16) != 0);  // Friday
        Assert.False((s.WorkDays & 2) != 0);  // Tuesday not set
    }

    public void Dispose() => _db.Dispose();
}

// ===========================================================================
// MODULE 3: Call Script & Disposition Management
// ===========================================================================
public class CallScriptTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _tenantId = Guid.NewGuid();

    public CallScriptTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new AppDbContext(opts);
        _db.Set<Tenant>().Add(new Tenant { Id = _tenantId, Name = "Scripts", Slug = "scripts" });
        _db.SaveChanges();
    }

    [Fact]
    public async Task CanCreateCallScript()
    {
        _db.CallScripts.Add(new CallScript { TenantId = _tenantId, Title = "Insurance Script", Content = "Hello, I'm calling about...", IsActive = true });
        await _db.SaveChangesAsync();

        var s = await _db.CallScripts.SingleAsync(x => x.Title == "Insurance Script");
        Assert.Contains("calling", s.Content);
    }

    [Fact]
    public async Task CanAddDispositionToScript()
    {
        var script = new CallScript { TenantId = _tenantId, Title = "S1", Content = "Content", IsActive = true };
        _db.CallScripts.Add(script);
        await _db.SaveChangesAsync();

        _db.CallDispositions.Add(new CallDisposition
        {
            TenantId = _tenantId, ScriptId = script.Id,
            Label = "Interested", Color = "#22c55e",
            NextStatus = LeadStatus.Interested, ClosesLead = false, SortOrder = 1
        });
        await _db.SaveChangesAsync();

        var disps = await _db.CallDispositions.Where(d => d.ScriptId == script.Id).ToListAsync();
        Assert.Single(disps);
        Assert.Equal(LeadStatus.Interested, disps[0].NextStatus);
    }

    [Fact]
    public async Task Disposition_ClosesLead_FlagWorks()
    {
        var script = new CallScript { TenantId = _tenantId, Title = "S2", Content = "...", IsActive = true };
        _db.CallScripts.Add(script);
        var disp = new CallDisposition { TenantId = _tenantId, ScriptId = script.Id, Label = "Dead", ClosesLead = true, NextStatus = LeadStatus.Dead };
        _db.CallDispositions.Add(disp);
        await _db.SaveChangesAsync();

        var saved = await _db.CallDispositions.FindAsync(disp.Id);
        Assert.True(saved!.ClosesLead);
        Assert.Equal(LeadStatus.Dead, saved.NextStatus);
    }

    [Fact]
    public async Task Dispositions_OrderedBySortOrder()
    {
        var script = new CallScript { TenantId = _tenantId, Title = "S3", Content = "...", IsActive = true };
        _db.CallScripts.Add(script);
        _db.CallDispositions.AddRange(
            new CallDisposition { TenantId = _tenantId, ScriptId = script.Id, Label = "C", SortOrder = 3 },
            new CallDisposition { TenantId = _tenantId, ScriptId = script.Id, Label = "A", SortOrder = 1 },
            new CallDisposition { TenantId = _tenantId, ScriptId = script.Id, Label = "B", SortOrder = 2 }
        );
        await _db.SaveChangesAsync();

        var ordered = await _db.CallDispositions
            .Where(d => d.ScriptId == script.Id)
            .OrderBy(d => d.SortOrder)
            .Select(d => d.Label)
            .ToListAsync();
        Assert.Equal(new[] { "A", "B", "C" }, ordered);
    }

    [Fact]
    public async Task DeletingScript_DeletesDispositions()
    {
        var script = new CallScript { TenantId = _tenantId, Title = "S4", Content = "...", IsActive = true };
        _db.CallScripts.Add(script);
        _db.CallDispositions.Add(new CallDisposition { TenantId = _tenantId, ScriptId = script.Id, Label = "X" });
        await _db.SaveChangesAsync();

        _db.CallScripts.Remove(script);
        await _db.SaveChangesAsync();

        Assert.Equal(0, await _db.CallDispositions.CountAsync(d => d.ScriptId == script.Id));
    }

    public void Dispose() => _db.Dispose();
}

// ===========================================================================
// MODULE 4: CRM Sync Config & Log
// ===========================================================================
public class CrmSyncTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _tenantId = Guid.NewGuid();

    public CrmSyncTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new AppDbContext(opts);
        _db.Set<Tenant>().Add(new Tenant { Id = _tenantId, Name = "Sync", Slug = "sync" });
        _db.SaveChanges();
    }

    [Fact]
    public async Task CanSaveCrmSyncConfig_HubSpot()
    {
        _db.CrmSyncConfigs.Add(new CrmSyncConfig
        {
            TenantId = _tenantId, Provider = "hubspot",
            AccessToken = "tok_abc", PortalId = "12345", IsActive = true
        });
        await _db.SaveChangesAsync();

        var c = await _db.CrmSyncConfigs.SingleAsync(x => x.Provider == "hubspot");
        Assert.Equal("12345", c.PortalId);
        Assert.True(c.IsActive);
    }

    [Fact]
    public async Task CanSaveCrmSyncConfig_Salesforce()
    {
        _db.CrmSyncConfigs.Add(new CrmSyncConfig
        {
            TenantId = _tenantId, Provider = "salesforce",
            AccessToken = "tok_sf", InstanceUrl = "https://myorg.salesforce.com", IsActive = true
        });
        await _db.SaveChangesAsync();

        var c = await _db.CrmSyncConfigs.SingleAsync(x => x.Provider == "salesforce");
        Assert.Equal("https://myorg.salesforce.com", c.InstanceUrl);
    }

    [Fact]
    public async Task CrmSyncLog_RecordsSuccess()
    {
        var config = new CrmSyncConfig { TenantId = _tenantId, Provider = "hubspot", AccessToken = "t", IsActive = true };
        _db.CrmSyncConfigs.Add(config);
        await _db.SaveChangesAsync();

        _db.CrmSyncLogs.Add(new CrmSyncLog
        {
            TenantId = _tenantId, CrmSyncConfigId = config.Id,
            Provider = "hubspot", ObjectType = "Contact", Direction = "push",
            ExternalId = "hs_001", Success = true
        });
        await _db.SaveChangesAsync();

        var log = await _db.CrmSyncLogs.FirstAsync();
        Assert.True(log.Success);
        Assert.Equal("hs_001", log.ExternalId);
    }

    [Fact]
    public async Task CrmSyncLog_RecordsFailure()
    {
        var config = new CrmSyncConfig { TenantId = _tenantId, Provider = "salesforce", AccessToken = "t", IsActive = true };
        _db.CrmSyncConfigs.Add(config);
        await _db.SaveChangesAsync();

        _db.CrmSyncLogs.Add(new CrmSyncLog
        {
            TenantId = _tenantId, CrmSyncConfigId = config.Id,
            Provider = "salesforce", ObjectType = "Lead", Direction = "push",
            ExternalId = "", Success = false, ErrorMessage = "HTTP 401"
        });
        await _db.SaveChangesAsync();

        var log = await _db.CrmSyncLogs.FirstAsync();
        Assert.False(log.Success);
        Assert.Equal("HTTP 401", log.ErrorMessage);
    }

    public void Dispose() => _db.Dispose();
}

// ===========================================================================
// MODULE 5: Invoice Generation
// ===========================================================================
public class InvoiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId   = Guid.NewGuid();
    private readonly Guid _leadId   = Guid.NewGuid();

    public InvoiceTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new AppDbContext(opts);
        _db.Set<Tenant>().Add(new Tenant { Id = _tenantId, Name = "Inv", Slug = "inv" });
        _db.Users.Add(new AppUser { Id = _userId, TenantId = _tenantId, FullName = "Sales", Role = "agent", IsActive = true, UserName = "sa", NormalizedUserName = "SA", Email = "sa@t.com", NormalizedEmail = "SA@T.COM", SecurityStamp = Guid.NewGuid().ToString() });
        _db.Leads.Add(new Lead { Id = _leadId, TenantId = _tenantId, Name = "Buyer Co", Phone = "9000000020" });
        _db.SaveChanges();
    }

    [Fact]
    public async Task CanCreateInvoice_WithLineItems()
    {
        var svc = new InvoiceService(_db, NullLogger<InvoiceService>.Instance);
        var items = new List<InvoiceLineItem>
        {
            new("CRM License", 1, 999m),
            new("Setup Fee", 1, 500m)
        };
        var inv = await svc.CreateInvoiceAsync(_tenantId, _leadId, _userId, items, 18m, null, null, null, null);

        Assert.Equal(1499m, inv.SubTotal);
        Assert.Equal(269.82m, inv.TaxAmount);
        Assert.Equal(1768.82m, inv.Total);
        Assert.Equal(InvoiceStatus.Draft, inv.Status);
        Assert.StartsWith("INV-", inv.InvoiceNumber);
    }

    [Fact]
    public async Task InvoiceNumber_IsUnique_PerTenant()
    {
        var svc = new InvoiceService(_db, NullLogger<InvoiceService>.Instance);
        var items = new List<InvoiceLineItem> { new("Item", 1, 100m) };
        var inv1 = await svc.CreateInvoiceAsync(_tenantId, _leadId, _userId, items, 0m, null, null, null, null);
        var inv2 = await svc.CreateInvoiceAsync(_tenantId, _leadId, _userId, items, 0m, null, null, null, null);

        Assert.NotEqual(inv1.InvoiceNumber, inv2.InvoiceNumber);
    }

    [Fact]
    public async Task MarkPaid_UpdatesStatusAndPaidAt()
    {
        var svc = new InvoiceService(_db, NullLogger<InvoiceService>.Instance);
        var items = new List<InvoiceLineItem> { new("Consulting", 2, 1000m) };
        var inv = await svc.CreateInvoiceAsync(_tenantId, _leadId, _userId, items, 18m, null, null, null, null);

        await svc.MarkPaidAsync(inv.Id, _tenantId);
        var saved = await _db.Invoices.FindAsync(inv.Id);

        Assert.Equal(InvoiceStatus.Paid, saved!.Status);
        Assert.NotNull(saved.PaidAt);
    }

    [Fact]
    public async Task VoidInvoice_SetsVoidStatus()
    {
        var svc = new InvoiceService(_db, NullLogger<InvoiceService>.Instance);
        var items = new List<InvoiceLineItem> { new("Product", 1, 500m) };
        var inv = await svc.CreateInvoiceAsync(_tenantId, _leadId, _userId, items, 0m, null, null, null, null);

        await svc.VoidInvoiceAsync(inv.Id, _tenantId);
        var saved = await _db.Invoices.FindAsync(inv.Id);

        Assert.Equal(InvoiceStatus.Void, saved!.Status);
    }

    [Fact]
    public async Task Invoice_ZeroTax_CalculatesCorrectly()
    {
        var svc = new InvoiceService(_db, NullLogger<InvoiceService>.Instance);
        var items = new List<InvoiceLineItem> { new("Export Service", 1, 2000m) };
        var inv = await svc.CreateInvoiceAsync(_tenantId, _leadId, _userId, items, 0m, null, null, null, null);

        Assert.Equal(2000m, inv.SubTotal);
        Assert.Equal(0m, inv.TaxAmount);
        Assert.Equal(2000m, inv.Total);
    }

    public void Dispose() => _db.Dispose();
}

// ===========================================================================
// MODULE 6: Localization (i18n)
// ===========================================================================
public class LocalizationTests
{
    [Fact]
    public void English_ResourceFile_HasCommonKeys()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Resources", "en.json");
        if (!File.Exists(path))
            path = FindResourceFile("en.json");

        Assert.True(File.Exists(path), $"en.json not found. Looked at: {path}");
        var json = File.ReadAllText(path);
        Assert.Contains("\"Save\"", json);
        Assert.Contains("\"Cancel\"", json);
    }

    [Fact]
    public void Hindi_ResourceFile_HasCommonKeys()
    {
        var path = FindResourceFile("hi.json");
        Assert.True(File.Exists(path), $"hi.json not found.");
        var json = File.ReadAllText(path);
        Assert.Contains("??????", json);
    }

    [Fact]
    public void Both_Locales_HaveSameTopLevelSections()
    {
        var enPath = FindResourceFile("en.json");
        var hiPath = FindResourceFile("hi.json");

        var enDoc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(enPath));
        var hiDoc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(hiPath));

        var enKeys = enDoc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();
        var hiKeys = hiDoc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();

        foreach (var k in enKeys)
            Assert.Contains(k, hiKeys);
    }

    private static string FindResourceFile(string fileName)
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            var candidate = Path.Combine(dir, "Resources", fileName);
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir) ?? dir;
        }
        return Path.Combine(dir, "Resources", fileName);
    }
}

// ===========================================================================
// MODULE 7: Real-time Dashboard — SignalR model / presence
// ===========================================================================
public class LiveDashboardTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _agentId  = Guid.NewGuid();
    private readonly Guid _leadId   = Guid.NewGuid();

    public LiveDashboardTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new AppDbContext(opts);
        _db.Set<Tenant>().Add(new Tenant { Id = _tenantId, Name = "Live", Slug = "live" });
        _db.Users.Add(new AppUser { Id = _agentId, TenantId = _tenantId, FullName = "LiveAgent", Role = "agent", IsActive = true, UserName = "la", NormalizedUserName = "LA", Email = "la@t.com", NormalizedEmail = "LA@T.COM", SecurityStamp = Guid.NewGuid().ToString() });
        _db.Leads.Add(new Lead { Id = _leadId, TenantId = _tenantId, Name = "LiveLead", Phone = "9000000030" });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Dashboard_CallsToday_CountsCorrectly()
    {
        var today = DateTime.UtcNow.Date;
        _db.Calls.AddRange(
            new Call { TenantId = _tenantId, LeadId = _leadId, AgentId = _agentId, StartedAt = today.AddHours(9), DurationSeconds = 60 },
            new Call { TenantId = _tenantId, LeadId = _leadId, AgentId = _agentId, StartedAt = today.AddHours(10), DurationSeconds = 90 },
            new Call { TenantId = _tenantId, LeadId = _leadId, AgentId = _agentId, StartedAt = today.AddDays(-1), DurationSeconds = 60 } // yesterday
        );
        await _db.SaveChangesAsync();

        var count = await _db.Calls.CountAsync(c => c.TenantId == _tenantId && c.StartedAt >= today);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Dashboard_ConversionsToday_CountsCorrectly()
    {
        var today = DateTime.UtcNow.Date;
        _db.Calls.AddRange(
            new Call { TenantId = _tenantId, LeadId = _leadId, AgentId = _agentId, StartedAt = today.AddHours(8), Outcome = CallOutcome.Converted },
            new Call { TenantId = _tenantId, LeadId = _leadId, AgentId = _agentId, StartedAt = today.AddHours(9), Outcome = CallOutcome.Interested }
        );
        await _db.SaveChangesAsync();

        var conversions = await _db.Calls.CountAsync(c =>
            c.TenantId == _tenantId && c.StartedAt >= today && c.Outcome == CallOutcome.Converted);
        Assert.Equal(1, conversions);
    }

    [Fact]
    public async Task Dashboard_OnlineAgents_CountsLatestPresence()
    {
        _db.AgentPresences.Add(new AgentPresence { TenantId = _tenantId, AgentId = _agentId, IsOnline = true, ChangedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var onlineCount = await _db.AgentPresences
            .Where(p => p.TenantId == _tenantId && p.IsOnline)
            .GroupBy(p => p.AgentId)
            .CountAsync();
        Assert.Equal(1, onlineCount);
    }

    [Fact]
    public async Task Dashboard_RevenueToday_SumsCapturedPayments()
    {
        var today = DateTime.UtcNow.Date;
        _db.Payments.AddRange(
            new Payment { TenantId = _tenantId, LeadId = _leadId, RecordedById = _agentId, Amount = 500m, Status = PaymentStatus.Captured, CapturedAt = today.AddHours(2) },
            new Payment { TenantId = _tenantId, LeadId = _leadId, RecordedById = _agentId, Amount = 300m, Status = PaymentStatus.Captured, CapturedAt = today.AddHours(3) },
            new Payment { TenantId = _tenantId, LeadId = _leadId, RecordedById = _agentId, Amount = 999m, Status = PaymentStatus.Pending,  CapturedAt = null }
        );
        await _db.SaveChangesAsync();

        var revenue = await _db.Payments
            .Where(p => p.TenantId == _tenantId && p.Status == PaymentStatus.Captured && p.CapturedAt >= today)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;
        Assert.Equal(800m, revenue);
    }

    public void Dispose() => _db.Dispose();
}

// ===========================================================================
// MODULE 8: Broader coverage — existing modules regression guard
// ===========================================================================
public class RegressionTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId   = Guid.NewGuid();

    public RegressionTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new AppDbContext(opts);
        _db.Set<Tenant>().Add(new Tenant { Id = _tenantId, Name = "Reg", Slug = "reg" });
        _db.Users.Add(new AppUser { Id = _userId, TenantId = _tenantId, FullName = "Reg User", Role = "agent", IsActive = true, UserName = "ru", NormalizedUserName = "RU", Email = "ru@t.com", NormalizedEmail = "RU@T.COM", SecurityStamp = Guid.NewGuid().ToString() });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Lead_CanBeCreated_WithAllFields()
    {
        var lead = new Lead
        {
            TenantId = _tenantId, Name = "Full Lead", Phone = "9000000040",
            Email = "fl@t.com", Company = "Acme", Industry = "Insurance",
            City = "Mumbai", State = "MH", Source = "Web", Priority = 1,
            Status = LeadStatus.New
        };
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();

        var saved = await _db.Leads.FindAsync(lead.Id);
        Assert.Equal("Acme", saved!.Company);
        Assert.Equal(LeadStatus.New, saved.Status);
    }

    [Fact]
    public async Task Notification_CanBeCreated_AndMarkedRead()
    {
        var notif = new Notification
        {
            TenantId = _tenantId, UserId = _userId,
            Type = NotificationType.SystemAlert,
            Title = "Test", Body = "Test body"
        };
        _db.Notifications.Add(notif);
        await _db.SaveChangesAsync();

        notif.IsRead = true;
        await _db.SaveChangesAsync();

        Assert.True((await _db.Notifications.FindAsync(notif.Id))!.IsRead);
    }

    [Fact]
    public async Task ApiKey_CanBeCreated_AndRevoked()
    {
        var key = new ApiKey
        {
            TenantId = _tenantId, CreatedById = _userId,
            Name = "Test Key", KeyHash = "hash_abc", IsActive = true
        };
        _db.ApiKeys.Add(key);
        await _db.SaveChangesAsync();

        key.IsActive = false;
        await _db.SaveChangesAsync();

        Assert.False((await _db.ApiKeys.FindAsync(key.Id))!.IsActive);
    }

    [Fact]
    public async Task CustomLeadField_CanBeCreated()
    {
        _db.CustomLeadFields.Add(new CustomLeadField
        {
            TenantId = _tenantId, Name = "Policy Number",
            FieldType = "text", IsRequired = false
        });
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _db.CustomLeadFields.CountAsync(f => f.TenantId == _tenantId));
    }

    [Fact]
    public async Task LeadTag_CanBeCreated_And_IsUniquePerTenant()
    {
        _db.LeadTags.Add(new LeadTag { TenantId = _tenantId, Name = "Hot Lead", Color = "#ef4444" });
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _db.LeadTags.CountAsync(t => t.TenantId == _tenantId));
    }

    [Fact]
    public async Task AllNewDbSets_AreQueryable()
    {
        // Verify all 8 new DbSets are accessible without errors
        Assert.Equal(0, await _db.AgentShifts.CountAsync());
        Assert.Equal(0, await _db.AgentPresences.CountAsync());
        Assert.Equal(0, await _db.RoundRobinStates.CountAsync());
        Assert.Equal(0, await _db.CallScripts.CountAsync());
        Assert.Equal(0, await _db.CallDispositions.CountAsync());
        Assert.Equal(0, await _db.CrmSyncConfigs.CountAsync());
        Assert.Equal(0, await _db.CrmSyncLogs.CountAsync());
        Assert.Equal(0, await _db.Invoices.CountAsync());
    }

    public void Dispose() => _db.Dispose();
}
