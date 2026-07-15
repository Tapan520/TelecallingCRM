using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Data;

/// <summary>
/// Seeds two demo tenants with users, campaigns, leads, calls and knowledge
/// chunks so the application can be fully validated without manual data entry.
///
/// Tenant 1 – "Apex Sales Co"   (slug: apex-sales)
///   admin   : admin@apexsales.com   / Admin@12345
///   manager : manager@apexsales.com / Manager@12345
///   agent1  : alice@apexsales.com   / Agent@12345
///   agent2  : bob@apexsales.com     / Agent@12345
///
/// Tenant 2 – "Nova Telecom"    (slug: nova-telecom)
///   admin   : admin@novatelecom.com / Admin@12345
///   agent   : raj@novatelecom.com   / Agent@12345
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        // Always ensure the platform superadmin exists (idempotent – runs even if tenants exist)
        if (await userManager.FindByEmailAsync("superadmin@telecallingcrm.com") == null)
        {
            var superAdmin = new AppUser
            {
                UserName = "superadmin@telecallingcrm.com",
                Email = "superadmin@telecallingcrm.com",
                FullName = "Platform SuperAdmin",
                TenantId = null,
                Role = "superadmin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var saResult = await userManager.CreateAsync(superAdmin, "SuperAdmin@12345");
            if (!saResult.Succeeded)
                throw new Exception("Could not create superadmin: " + string.Join(", ", saResult.Errors.Select(e => e.Description)));
            // Explicitly persist Role after creation to ensure it's saved
            superAdmin.Role = "superadmin";
            await userManager.UpdateAsync(superAdmin);
        }
        else
        {
            // Fix any existing superadmin with blank role
            var existing = await userManager.FindByEmailAsync("superadmin@telecallingcrm.com");
            if (existing != null && string.IsNullOrEmpty(existing.Role))
            {
                existing.Role = "superadmin";
                await userManager.UpdateAsync(existing);
            }
        }

        // Skip the rest if demo tenant data already exists
        if (await db.Tenants.AnyAsync()) return;

        // ?? TENANT 1 ??????????????????????????????????????????????????????????
        var apex = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Apex Sales Co",
            Slug = "apex-sales",
            Plan = "pro",
            IsActive = true,
            MaxUsers = 20,
            MaxLeads = 2000,
            PreferredModel = "openai/gpt-4o-mini",
            CreatedAt = DateTime.UtcNow.AddDays(-90)
        };

        // ?? TENANT 2 ??????????????????????????????????????????????????????????
        var nova = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Nova Telecom",
            Slug = "nova-telecom",
            Plan = "starter",
            IsActive = true,
            MaxUsers = 5,
            MaxLeads = 500,
            PreferredModel = "openai/gpt-4o-mini",
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };

        db.Tenants.AddRange(apex, nova);
        await db.SaveChangesAsync();

        // ?? APEX USERS ????????????????????????????????????????????????????????
        var apexAdmin   = await CreateUser(userManager, apex.Id, "Sarah Johnson",   "admin@apexsales.com",   "Admin@12345",   "admin");
        var apexManager = await CreateUser(userManager, apex.Id, "David Chen",      "manager@apexsales.com", "Manager@12345", "manager");
        var apexAlice   = await CreateUser(userManager, apex.Id, "Alice Fernandez", "alice@apexsales.com",   "Agent@12345",   "agent");
        var apexBob     = await CreateUser(userManager, apex.Id, "Bob Nair",        "bob@apexsales.com",     "Agent@12345",   "agent");

        // ?? NOVA USERS ????????????????????????????????????????????????????????
        var novaAdmin = await CreateUser(userManager, nova.Id, "Priya Patel",  "admin@novatelecom.com", "Admin@12345", "admin");
        var novaRaj   = await CreateUser(userManager, nova.Id, "Raj Sharma",   "raj@novatelecom.com",   "Agent@12345", "agent");

        // ?? APEX CAMPAIGNS ????????????????????????????????????????????????????
        var c1 = new Campaign
        {
            Id = Guid.NewGuid(), TenantId = apex.Id,
            Name = "Q3 Enterprise Drive",
            Description = "Target mid-market enterprise accounts in the APAC region.",
            Status = CampaignStatus.Active,
            Script = "Hi, I'm calling from Apex Sales. We help companies reduce their customer acquisition cost by up to 40%. Do you have 3 minutes to hear how?",
            StartDate = DateTime.UtcNow.AddDays(-60),
            EndDate = DateTime.UtcNow.AddDays(30),
            TargetCallsPerDay = 50,
            CreatedAt = DateTime.UtcNow.AddDays(-60)
        };

        var c2 = new Campaign
        {
            Id = Guid.NewGuid(), TenantId = apex.Id,
            Name = "SMB Upsell – Premium Plan",
            Description = "Upsell existing SMB clients to the premium tier.",
            Status = CampaignStatus.Active,
            Script = "Hi [Name], this is [Agent] from Apex. You've been on our starter plan for 6 months – I'd love to show you what the premium plan can unlock for your team.",
            StartDate = DateTime.UtcNow.AddDays(-20),
            EndDate = DateTime.UtcNow.AddDays(40),
            TargetCallsPerDay = 30,
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        };

        var c3 = new Campaign
        {
            Id = Guid.NewGuid(), TenantId = apex.Id,
            Name = "Churn Rescue 2024",
            Description = "Re-engage customers who cancelled in the last 90 days.",
            Status = CampaignStatus.Paused,
            Script = "Hi [Name], we noticed you cancelled your subscription. We've made improvements since then – would you give us another chance?",
            StartDate = DateTime.UtcNow.AddDays(-10),
            TargetCallsPerDay = 20,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };

        // ?? NOVA CAMPAIGNS ????????????????????????????????????????????????????
        var c4 = new Campaign
        {
            Id = Guid.NewGuid(), TenantId = nova.Id,
            Name = "Broadband Launch – Tier 1 Cities",
            Description = "New broadband product launch targeting urban households.",
            Status = CampaignStatus.Active,
            Script = "Hello, I'm calling from Nova Telecom. We've just launched a 1 Gbps broadband plan at half the market price in your area. Are you interested?",
            StartDate = DateTime.UtcNow.AddDays(-15),
            EndDate = DateTime.UtcNow.AddDays(45),
            TargetCallsPerDay = 80,
            CreatedAt = DateTime.UtcNow.AddDays(-15)
        };

        db.Campaigns.AddRange(c1, c2, c3, c4);
        await db.SaveChangesAsync();

        // ?? APEX LEADS ????????????????????????????????????????????????????????
        var apexLeads = new List<Lead>
        {
            MakeLead(apex.Id, apexAlice.Id, c1.Id,  "Rajesh Kumar",    "+91-9810012345", "rajesh.kumar@techcorp.in",      "TechCorp India",         LeadStatus.Interested,    1, "LinkedIn",    "Attended webinar on Q2 product demo."),
            MakeLead(apex.Id, apexAlice.Id, c1.Id,  "Sunita Mehta",    "+91-9920023456", "sunita@innovatesoft.com",       "InnovateSoft",           LeadStatus.FollowUp,      1, "Cold Call",   "Requested a callback next Monday."),
            MakeLead(apex.Id, apexBob.Id,   c1.Id,  "James O'Brien",   "+353-87-1234567","james@atlanticdigi.ie",         "Atlantic Digital",       LeadStatus.Contacted,     0, "Referral",    "Referred by existing client."),
            MakeLead(apex.Id, apexBob.Id,   c1.Id,  "Li Wei",          "+86-13812345678","liwei@shenzhentech.cn",         "Shenzhen Tech",          LeadStatus.New,           0, "Website",     null),
            MakeLead(apex.Id, apexAlice.Id, c2.Id,  "Meera Iyer",      "+91-9876543210", "meera@startup42.com",           "Startup42",              LeadStatus.Converted,     2, "Inbound",     "Upgraded to premium. Very happy!"),
            MakeLead(apex.Id, apexAlice.Id, c2.Id,  "Tom Steiner",     "+49-151-12345678","tom.steiner@fabricate.de",     "Fabricate GmbH",         LeadStatus.NotInterested, 0, "Email",       "Budget constraints cited."),
            MakeLead(apex.Id, apexBob.Id,   c2.Id,  "Anita Desai",     "+91-9123456789", "anita@cloudvision.io",          "CloudVision",            LeadStatus.FollowUp,      1, "LinkedIn",    "Demo scheduled for this Friday."),
            MakeLead(apex.Id, apexBob.Id,   c3.Id,  "Carlos Reyes",    "+52-55-12345678","carlos.reyes@mexisolutions.mx", "MexiSolutions",          LeadStatus.New,           0, "Cancellation","Cancelled 3 months ago – price issue."),
            MakeLead(apex.Id, apexAlice.Id, c3.Id,  "Emma Wilson",     "+44-7700-900123","emma.wilson@londonbiz.co.uk",   "LondonBiz Ltd",          LeadStatus.Contacted,     1, "Cancellation","Was unhappy with support."),
            MakeLead(apex.Id, apexManager.Id, null, "Hiroshi Tanaka",  "+81-90-1234-5678","hiroshi@tokyoventures.jp",     "Tokyo Ventures",         LeadStatus.Interested,    2, "Conference",  "Met at SaaS Summit Tokyo."),
            MakeLead(apex.Id, apexBob.Id,   c1.Id,  "Fatima Al-Rashid","+971-50-1234567","fatima@dubaiholdings.ae",       "Dubai Holdings",         LeadStatus.New,           0, "Website",     null),
            MakeLead(apex.Id, apexAlice.Id, c2.Id,  "Miguel Santos",   "+55-11-91234-5678","miguel@brasiltec.com.br",     "BrasilTec",              LeadStatus.Dead,          0, "Cold Call",   "Not a fit. Wrong industry."),
            MakeLead(apex.Id, apexBob.Id,   c1.Id,  "Priya Krishnan",  "+65-8123-4567",  "priya@sglabs.sg",              "SG Labs",                LeadStatus.Converted,     2, "Referral",    "Closed deal. 1-year contract signed."),
            MakeLead(apex.Id, apexAlice.Id, null,   "Daniel Okonkwo",  "+234-803-1234567","daniel@lagosinno.ng",          "Lagos Innovations",      LeadStatus.New,           0, "LinkedIn",    null),
            MakeLead(apex.Id, apexBob.Id,   c2.Id,  "Yuki Hayashi",    "+81-80-2345-6789","yuki@osaka-media.jp",          "Osaka Media",            LeadStatus.FollowUp,      1, "Email",       "Needs approval from board."),
        };

        // ?? NOVA LEADS ????????????????????????????????????????????????????????
        var novaLeads = new List<Lead>
        {
            MakeLead(nova.Id, novaRaj.Id,   c4.Id,  "Arun Pillai",     "+91-9811122334", "arun.pillai@home.in",           null,                     LeadStatus.Interested,    1, "Cold Call",   "Interested in 1 Gbps plan."),
            MakeLead(nova.Id, novaRaj.Id,   c4.Id,  "Deepa Nair",      "+91-9822233445", "deepa.nair@gmail.com",          null,                     LeadStatus.Converted,     2, "Inbound",     "Signed up for the annual plan."),
            MakeLead(nova.Id, novaRaj.Id,   c4.Id,  "Vikram Bose",     "+91-9833344556", null,                            null,                     LeadStatus.NotInterested, 0, "Cold Call",   "Already has a provider."),
            MakeLead(nova.Id, novaRaj.Id,   c4.Id,  "Neha Gupta",      "+91-9844455667", "neha@startupbox.in",            "StartupBox",             LeadStatus.FollowUp,      1, "Website",     "Wants to discuss business plans."),
            MakeLead(nova.Id, novaAdmin.Id, c4.Id,  "Sanjay Malhotra", "+91-9855566778", "sanjay.m@techpark.in",          "TechPark",               LeadStatus.New,           0, "Referral",    null),
        };

        db.Leads.AddRange(apexLeads);
        db.Leads.AddRange(novaLeads);
        await db.SaveChangesAsync();

        // ?? CALLS ?????????????????????????????????????????????????????????????
        var calls = new List<Call>();
        var rng = new Random(42);

        // Generate realistic calls for apex leads
        foreach (var lead in apexLeads.Take(10))
        {
            var callCount = rng.Next(1, 4);
            for (var i = 0; i < callCount; i++)
            {
                var agentId = (i % 2 == 0) ? apexAlice.Id : apexBob.Id;
                var started = lead.CreatedAt.AddDays(i + rng.Next(0, 3)).AddHours(rng.Next(9, 18));
                var duration = rng.Next(30, 600);
                var outcome = lead.Status switch
                {
                    LeadStatus.Converted     => CallOutcome.Converted,
                    LeadStatus.NotInterested => CallOutcome.NotInterested,
                    LeadStatus.Interested    => CallOutcome.Interested,
                    LeadStatus.FollowUp      => CallOutcome.Callback,
                    LeadStatus.Contacted     => CallOutcome.Interested,
                    _                        => (CallOutcome)rng.Next(0, 8)
                };

                calls.Add(new Call
                {
                    Id = Guid.NewGuid(),
                    TenantId = apex.Id,
                    LeadId = lead.Id,
                    AgentId = agentId,
                    StartedAt = started,
                    EndedAt = started.AddSeconds(duration),
                    DurationSeconds = duration,
                    Outcome = outcome,
                    Notes = SampleCallNote(outcome),
                    AiSentiment = duration > 300 ? "positive" : duration > 120 ? "neutral" : "negative",
                    AiSummary = $"Agent discussed offering with {lead.Name}. Outcome: {outcome}.",
                    CreatedAt = started
                });
            }
        }

        // Generate calls for nova leads
        foreach (var lead in novaLeads)
        {
            var started = lead.CreatedAt.AddHours(rng.Next(9, 18));
            var duration = rng.Next(60, 480);
            var outcome = lead.Status switch
            {
                LeadStatus.Converted     => CallOutcome.Converted,
                LeadStatus.NotInterested => CallOutcome.NotInterested,
                LeadStatus.Interested    => CallOutcome.Interested,
                LeadStatus.FollowUp      => CallOutcome.Callback,
                _                        => CallOutcome.NoAnswer
            };

            calls.Add(new Call
            {
                Id = Guid.NewGuid(),
                TenantId = nova.Id,
                LeadId = lead.Id,
                AgentId = novaRaj.Id,
                StartedAt = started,
                EndedAt = started.AddSeconds(duration),
                DurationSeconds = duration,
                Outcome = outcome,
                Notes = SampleCallNote(outcome),
                AiSentiment = duration > 240 ? "positive" : "neutral",
                AiSummary = $"Call with {lead.Name} regarding Nova broadband launch. Outcome: {outcome}.",
                CreatedAt = started
            });
        }

        db.Calls.AddRange(calls);

        // ?? KNOWLEDGE CHUNKS ??????????????????????????????????????????????????
        var apexKnowledge = new List<KnowledgeChunk>
        {
            new() { Id = Guid.NewGuid(), TenantId = apex.Id, Category = "Pricing",
                Title = "Apex Sales Pricing Tiers",
                Content = "Starter: $49/month (up to 3 users, 200 leads). Pro: $149/month (up to 20 users, 2000 leads, AI features). Enterprise: Custom pricing with dedicated support and SLA.",
                CreatedAt = DateTime.UtcNow.AddDays(-80) },
            new() { Id = Guid.NewGuid(), TenantId = apex.Id, Category = "Objections",
                Title = "Handling Price Objections",
                Content = "When a prospect says the price is too high, focus on ROI: 'Our average customer sees a 3x return within 90 days. Let me share a case study from a similar company.' Then offer a 14-day free trial to reduce risk.",
                CreatedAt = DateTime.UtcNow.AddDays(-75) },
            new() { Id = Guid.NewGuid(), TenantId = apex.Id, Category = "Product",
                Title = "Key Product Differentiators",
                Content = "1. AI-powered lead scoring saves 2 hours/day per agent. 2. Real-time call analytics with sentiment detection. 3. Seamless CRM integrations (Salesforce, HubSpot). 4. GDPR & SOC2 Type II compliant.",
                CreatedAt = DateTime.UtcNow.AddDays(-70) },
            new() { Id = Guid.NewGuid(), TenantId = apex.Id, Category = "Scripts",
                Title = "Cold Call Opening Script",
                Content = "Hi [Name], this is [Agent] from Apex Sales. I'll be quick – we help B2B sales teams close 30% more deals using AI-assisted calling. Have you ever felt your team spends too much time on admin rather than selling?",
                CreatedAt = DateTime.UtcNow.AddDays(-65) },
            new() { Id = Guid.NewGuid(), TenantId = apex.Id, Category = "FAQ",
                Title = "Frequently Asked Questions",
                Content = "Q: Is there a free trial? A: Yes, 14 days, no credit card required. Q: Can I import existing leads? A: Yes, CSV and API import supported. Q: Is data secure? A: AES-256 encryption, daily backups, SOC2 certified.",
                CreatedAt = DateTime.UtcNow.AddDays(-60) },
        };

        var novaKnowledge = new List<KnowledgeChunk>
        {
            new() { Id = Guid.NewGuid(), TenantId = nova.Id, Category = "Plans",
                Title = "Nova Broadband Plans",
                Content = "Home Basic: 100 Mbps – ?599/month. Home Plus: 500 Mbps – ?999/month. Home Ultra: 1 Gbps – ?1499/month. All plans include free router, unlimited data, and 24/7 support.",
                CreatedAt = DateTime.UtcNow.AddDays(-28) },
            new() { Id = Guid.NewGuid(), TenantId = nova.Id, Category = "Objections",
                Title = "Handling 'I Already Have a Provider' Objection",
                Content = "Acknowledge their current provider, then ask: 'What speed are you currently getting and are you satisfied?' If they mention any pain point, position Nova's speed and price. Offer a 30-day free trial with no cancellation fee.",
                CreatedAt = DateTime.UtcNow.AddDays(-25) },
            new() { Id = Guid.NewGuid(), TenantId = nova.Id, Category = "Installation",
                Title = "Installation Process FAQ",
                Content = "Installation is free for all new subscribers. A Nova technician visits within 48 hours of sign-up. The process takes approximately 2 hours. The router is pre-configured. Customer just needs to be home during the 2-hour slot.",
                CreatedAt = DateTime.UtcNow.AddDays(-20) },
        };

        db.KnowledgeChunks.AddRange(apexKnowledge);
        db.KnowledgeChunks.AddRange(novaKnowledge);

        await db.SaveChangesAsync();
    }

    // ?? Helpers ???????????????????????????????????????????????????????????????

    private static async Task<AppUser> CreateUser(
        UserManager<AppUser> userManager,
        Guid tenantId, string fullName, string email, string password, string role)
    {
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            TenantId = tenantId,
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            LastLoginAt = DateTime.UtcNow.AddHours(-new Random().Next(1, 72))
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new Exception($"Could not create seed user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        // Explicitly update to ensure custom columns (Role, FullName, IsActive) are persisted
        user.Role = role;
        await userManager.UpdateAsync(user);
        return user;
    }

    private static readonly Random _rng = new(42);

    private static Lead MakeLead(
        Guid tenantId, Guid assignedToId, Guid? campaignId,
        string name, string phone, string? email, string? company,
        LeadStatus status, int priority, string? source, string? notes)
    {
        var daysAgo = _rng.Next(2, 80);
        return new Lead
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AssignedToId = assignedToId,
            CampaignId = campaignId,
            Name = name,
            Phone = phone,
            Email = email,
            Company = company,
            Status = status,
            Priority = priority,
            Source = source,
            Notes = notes,
            CreatedAt = DateTime.UtcNow.AddDays(-daysAgo),
            UpdatedAt = DateTime.UtcNow.AddDays(-_rng.Next(0, daysAgo)),
            NextFollowUpAt = status == LeadStatus.FollowUp ? DateTime.UtcNow.AddDays(_rng.Next(1, 7)) : null
        };
    }

    private static string SampleCallNote(CallOutcome outcome) => outcome switch
    {
        CallOutcome.Converted     => "Deal closed. Contract sent via email.",
        CallOutcome.Interested    => "Very engaged. Wants a detailed proposal.",
        CallOutcome.Callback      => "Asked for callback at a later time.",
        CallOutcome.NotInterested => "Politely declined. Not the right time.",
        CallOutcome.NoAnswer      => "No answer. Left voicemail.",
        CallOutcome.Busy          => "Line busy. Will retry tomorrow.",
        CallOutcome.WrongNumber   => "Wrong number. Remove from list.",
        _                         => "Call completed."
    };
}
