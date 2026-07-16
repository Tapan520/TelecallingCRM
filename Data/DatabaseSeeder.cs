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

        // ?? FOLLOW-UPS ????????????????????????????????????????????????????????
        var followUps = new List<FollowUp>
        {
            new() { TenantId = apex.Id, LeadId = apexLeads[1].Id, AssignedToId = apexAlice.Id,
                    ScheduledAt = DateTime.UtcNow.AddDays(1), Channel = FollowUpChannel.Call,
                    Notes = "Callback as requested. Confirm demo time.", Status = FollowUpStatus.Pending },
            new() { TenantId = apex.Id, LeadId = apexLeads[6].Id, AssignedToId = apexBob.Id,
                    ScheduledAt = DateTime.UtcNow.AddDays(3), Channel = FollowUpChannel.WhatsApp,
                    Notes = "Send demo recording and pricing PDF.", Status = FollowUpStatus.Pending },
            new() { TenantId = apex.Id, LeadId = apexLeads[9].Id, AssignedToId = apexManager.Id,
                    ScheduledAt = DateTime.UtcNow.AddDays(-1), Channel = FollowUpChannel.Email,
                    Notes = "Send conference follow-up email with proposal.", Status = FollowUpStatus.Missed },
            new() { TenantId = nova.Id,  LeadId = novaLeads[3].Id, AssignedToId = novaRaj.Id,
                    ScheduledAt = DateTime.UtcNow.AddDays(2), Channel = FollowUpChannel.Call,
                    Notes = "Discuss business broadband plan options.", Status = FollowUpStatus.Pending },
        };
        db.FollowUps.AddRange(followUps);

        // ?? TASKS ?????????????????????????????????????????????????????????????
        var tasks = new List<TaskItem>
        {
            new() { TenantId = apex.Id, LeadId = apexLeads[4].Id,
                    AssignedToId = apexAlice.Id, CreatedById = apexAdmin.Id,
                    Title = "Send signed contract to Meera Iyer",
                    Description = "Email the countersigned MSA and onboarding guide.",
                    Priority = TaskPriority.High,
                    DueAt = DateTime.UtcNow.AddHours(4),
                    Status = TelecallingCRM.Data.Models.TaskStatus.Pending },
            new() { TenantId = apex.Id, LeadId = apexLeads[0].Id,
                    AssignedToId = apexBob.Id, CreatedById = apexManager.Id,
                    Title = "Prepare product demo for Rajesh Kumar",
                    Description = "Custom demo showing enterprise dashboard and AI scoring.",
                    Priority = TaskPriority.Normal,
                    DueAt = DateTime.UtcNow.AddDays(2),
                    Status = TelecallingCRM.Data.Models.TaskStatus.Pending },
            new() { TenantId = apex.Id,
                    AssignedToId = apexManager.Id, CreatedById = apexAdmin.Id,
                    Title = "Review Q3 campaign performance report",
                    Description = "Analyse conversion rates and prepare summary for the team.",
                    Priority = TaskPriority.Normal,
                    DueAt = DateTime.UtcNow.AddDays(5),
                    Status = TelecallingCRM.Data.Models.TaskStatus.Pending },
            new() { TenantId = apex.Id, LeadId = apexLeads[12].Id,
                    AssignedToId = apexAlice.Id, CreatedById = apexAdmin.Id,
                    Title = "Process onboarding for Priya Krishnan",
                    Description = "Set up tenant account and send welcome kit.",
                    Priority = TaskPriority.High,
                    DueAt = DateTime.UtcNow.AddHours(-2),
                    Status = TelecallingCRM.Data.Models.TaskStatus.Overdue },
            new() { TenantId = nova.Id, LeadId = novaLeads[1].Id,
                    AssignedToId = novaRaj.Id, CreatedById = novaAdmin.Id,
                    Title = "Confirm installation slot for Deepa Nair",
                    Description = "Book technician visit within 48 hours as promised.",
                    Priority = TaskPriority.High,
                    DueAt = DateTime.UtcNow.AddDays(1),
                    Status = TelecallingCRM.Data.Models.TaskStatus.Pending },
        };
        db.Tasks.AddRange(tasks);

        // ?? MEETINGS ??????????????????????????????????????????????????????????
        var meeting1 = new Meeting
        {
            TenantId = apex.Id, LeadId = apexLeads[0].Id, OrganisedById = apexAlice.Id,
            Title = "Product Demo — Rajesh Kumar", Type = MeetingType.VideoCall,
            ScheduledAt = DateTime.UtcNow.AddDays(2).Date.AddHours(11),
            DurationMinutes = 45, Status = MeetingStatus.Scheduled,
            MeetingLink = "https://meet.google.com/abc-defg-hij",
            Agenda = "Walk through enterprise dashboard, AI scoring, and integration options."
        };
        var meeting2 = new Meeting
        {
            TenantId = apex.Id, LeadId = apexLeads[9].Id, OrganisedById = apexManager.Id,
            Title = "Strategy Call — Tokyo Ventures", Type = MeetingType.VideoCall,
            ScheduledAt = DateTime.UtcNow.AddDays(5).Date.AddHours(14),
            DurationMinutes = 60, Status = MeetingStatus.Scheduled,
            Agenda = "Discuss APAC expansion and enterprise licensing terms."
        };
        var meeting3 = new Meeting
        {
            TenantId = apex.Id, LeadId = apexLeads[4].Id, OrganisedById = apexAlice.Id,
            Title = "Onboarding Session — Meera Iyer", Type = MeetingType.VideoCall,
            ScheduledAt = DateTime.UtcNow.AddDays(-3).Date.AddHours(10),
            DurationMinutes = 30, Status = MeetingStatus.Completed,
            Outcome = "Onboarding completed. Meera is live on the platform.",
            Agenda = "Walk through the portal and set up first campaign."
        };
        var meeting4 = new Meeting
        {
            TenantId = nova.Id, LeadId = novaLeads[0].Id, OrganisedById = novaRaj.Id,
            Title = "Plan Walkthrough — Arun Pillai", Type = MeetingType.PhoneCall,
            ScheduledAt = DateTime.UtcNow.AddDays(1).Date.AddHours(16),
            DurationMinutes = 20, Status = MeetingStatus.Scheduled,
            Agenda = "Explain Home Ultra 1 Gbps plan and installation process."
        };
        db.Meetings.AddRange(meeting1, meeting2, meeting3, meeting4);
        db.MeetingAttendees.AddRange(
            new MeetingAttendee { Meeting = meeting1, UserId = apexAlice.Id },
            new MeetingAttendee { Meeting = meeting2, UserId = apexManager.Id },
            new MeetingAttendee { Meeting = meeting2, UserId = apexAlice.Id },
            new MeetingAttendee { Meeting = meeting3, UserId = apexAlice.Id },
            new MeetingAttendee { Meeting = meeting4, UserId = novaRaj.Id }
        );

        // ?? PAYMENTS ?????????????????????????????????????????????????????????
        db.Payments.AddRange(
            new Payment
            {
                TenantId = apex.Id, LeadId = apexLeads[4].Id, RecordedById = apexAlice.Id,
                Amount = 14988m, Currency = "USD", Status = PaymentStatus.Captured,
                Description = "Annual Pro Plan — Meera Iyer / Startup42",
                ReceiptNumber = "RCPT-001", CapturedAt = DateTime.UtcNow.AddDays(-2),
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new Payment
            {
                TenantId = apex.Id, LeadId = apexLeads[12].Id, RecordedById = apexAlice.Id,
                Amount = 17880m, Currency = "USD", Status = PaymentStatus.Captured,
                Description = "1-Year Enterprise Contract — Priya Krishnan / SG Labs",
                ReceiptNumber = "RCPT-002",
                RazorpayOrderId = "order_demo_sglab_001",
                RazorpayPaymentId = "pay_demo_sglab_001",
                CapturedAt = DateTime.UtcNow.AddDays(-5), CreatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new Payment
            {
                TenantId = apex.Id, LeadId = apexLeads[2].Id, RecordedById = apexBob.Id,
                Amount = 1788m, Currency = "EUR", Status = PaymentStatus.Pending,
                Description = "Starter Plan — James O'Brien / Atlantic Digital",
                RazorpayOrderId = "order_demo_atlantic_001",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new Payment
            {
                TenantId = nova.Id, LeadId = novaLeads[1].Id, RecordedById = novaRaj.Id,
                Amount = 17988m, Currency = "INR", Status = PaymentStatus.Captured,
                Description = "Annual Home Ultra Plan — Deepa Nair",
                ReceiptNumber = "NOVA-RCPT-001",
                CapturedAt = DateTime.UtcNow.AddDays(-1), CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        );

        // ?? DNC ENTRIES ???????????????????????????????????????????????????????
        db.DncEntries.AddRange(
            new DncEntry { TenantId = apex.Id, Phone = "919999900000", Reason = "Customer requested opt-out", AddedById = apexAdmin.Id },
            new DncEntry { TenantId = apex.Id, Phone = "918888800000", Reason = "DNC registry", AddedById = apexManager.Id },
            new DncEntry { TenantId = nova.Id, Phone = "917777700000", Reason = "Complaint raised", AddedById = novaAdmin.Id }
        );

        // ?? SMS TEMPLATES ?????????????????????????????????????????????????????
        db.SmsTemplates.AddRange(
            new SmsTemplate
            {
                TenantId = apex.Id, Name = "Follow-up Reminder",
                Body = "Hi {{lead_name}}, this is {{agent_name}} from Apex Sales. Just following up on our conversation. Reply YES to schedule a quick call. Thanks!",
                Category = "followup", IsActive = true
            },
            new SmsTemplate
            {
                TenantId = apex.Id, Name = "Meeting Confirmation",
                Body = "Hi {{lead_name}}, your demo with Apex Sales is confirmed for {{date}} at {{time}}. Join link: https://meet.example.com/demo — {{agent_name}}",
                Category = "meeting", IsActive = true
            },
            new SmsTemplate
            {
                TenantId = nova.Id, Name = "Installation Reminder",
                Body = "Dear {{lead_name}}, your Nova Telecom technician visit is scheduled for tomorrow. Please ensure someone is home between 10am-12pm. Call {{phone}} to reschedule.",
                Category = "installation", IsActive = true
            }
        );

        // ?? WHATSAPP TEMPLATES ????????????????????????????????????????????????
        db.WhatsAppTemplates.AddRange(
            new WhatsAppTemplate
            {
                TenantId = apex.Id, Name = "Proposal Sent",
                TemplateName = "proposal_sent_v1", Language = "en",
                BodyPreview = "Hello {{1}}, I've just sent over the proposal for your review. Please check your email and let me know if you have questions. — {{2}}, Apex Sales",
                Category = "UTILITY", IsActive = true
            },
            new WhatsAppTemplate
            {
                TenantId = apex.Id, Name = "Trial Expiry Reminder",
                TemplateName = "trial_expiry_v1", Language = "en",
                BodyPreview = "Hi {{1}}, your 14-day free trial ends in 3 days. Upgrade now to keep all your leads and data. Use code EARLYBIRD for 20% off.",
                Footer = "Reply STOP to unsubscribe", Category = "MARKETING", IsActive = true
            },
            new WhatsAppTemplate
            {
                TenantId = nova.Id, Name = "Plan Activation",
                TemplateName = "plan_activation_v1", Language = "en",
                BodyPreview = "Dear {{1}}, your Nova Telecom {{2}} plan has been activated! Your internet is now live. For support call 1800-NOVA-HELP.",
                Category = "UTILITY", IsActive = true
            }
        );

        // ?? EMAIL TEMPLATES ???????????????????????????????????????????????????
        db.EmailTemplates.AddRange(
            new EmailTemplate
            {
                TenantId = apex.Id, Name = "Welcome & Onboarding",
                Subject = "Welcome to Apex Sales — Let's get you started!",
                Body = "<h2>Welcome, {{lead_name}}!</h2><p>Thank you for choosing Apex Sales. Your account is ready.<br>Your dedicated agent is <strong>{{agent_name}}</strong>.<br>Reply to this email or call us anytime.</p>",
                Category = "onboarding"
            },
            new EmailTemplate
            {
                TenantId = apex.Id, Name = "Follow-up After Demo",
                Subject = "Great speaking with you, {{lead_name}}!",
                Body = "<p>Hi {{lead_name}},</p><p>It was great showing you our platform today. As discussed, I've attached the proposal and pricing sheet.</p><p>Let me know if you have any questions!</p><p>Best,<br>{{agent_name}}</p>",
                Category = "followup"
            },
            new EmailTemplate
            {
                TenantId = nova.Id, Name = "Installation Confirmation",
                Subject = "Your Nova Telecom Installation is Confirmed",
                Body = "<p>Dear {{lead_name}},</p><p>Your installation appointment is confirmed. A technician will visit tomorrow between 10am–12pm.</p><p>Thanks for choosing Nova Telecom!</p>",
                Category = "installation"
            }
        );

        // ?? AGENT GOALS ???????????????????????????????????????????????????????
        var goalStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var goalEnd   = goalStart.AddMonths(1).AddDays(-1);
        db.AgentGoals.AddRange(
            new AgentGoal
            {
                TenantId = apex.Id, AgentId = apexAlice.Id, CreatedById = apexManager.Id,
                Label = $"{DateTime.UtcNow:MMMM yyyy} — Alice",
                TargetCalls = 80, TargetConversions = 8, TargetTalkSeconds = 28800, TargetFollowUps = 30,
                PeriodStart = goalStart, PeriodEnd = goalEnd, IsActive = true
            },
            new AgentGoal
            {
                TenantId = apex.Id, AgentId = apexBob.Id, CreatedById = apexManager.Id,
                Label = $"{DateTime.UtcNow:MMMM yyyy} — Bob",
                TargetCalls = 70, TargetConversions = 6, TargetTalkSeconds = 25200, TargetFollowUps = 25,
                PeriodStart = goalStart, PeriodEnd = goalEnd, IsActive = true
            },
            new AgentGoal
            {
                TenantId = nova.Id, AgentId = novaRaj.Id, CreatedById = novaAdmin.Id,
                Label = $"{DateTime.UtcNow:MMMM yyyy} — Raj",
                TargetCalls = 120, TargetConversions = 15, TargetTalkSeconds = 36000, TargetFollowUps = 40,
                PeriodStart = goalStart, PeriodEnd = goalEnd, IsActive = true
            }
        );

        // ?? ESCALATION RULES ?????????????????????????????????????????????????
        db.EscalationRules.AddRange(
            new EscalationRule
            {
                TenantId = apex.Id, Name = "3 Missed Follow-ups",
                Trigger = EscalationTrigger.MissedFollowUp, ThresholdValue = 3,
                EscalateToId = apexManager.Id, IsActive = true
            },
            new EscalationRule
            {
                TenantId = apex.Id, Name = "No Contact — 7 Days",
                Trigger = EscalationTrigger.NoContactDays, ThresholdValue = 7,
                EscalateToId = apexManager.Id, IsActive = true
            },
            new EscalationRule
            {
                TenantId = nova.Id, Name = "No Contact — 5 Days",
                Trigger = EscalationTrigger.NoContactDays, ThresholdValue = 5,
                EscalateToId = novaAdmin.Id, IsActive = true
            }
        );
        await db.SaveChangesAsync();

        // ?? ESCALATION INSTANCES ??????????????????????????????????????????????
        db.Escalations.AddRange(
            new Escalation
            {
                TenantId = apex.Id, LeadId = apexLeads[7].Id,
                AssignedAgentId = apexBob.Id, EscalatedToId = apexManager.Id,
                Status = EscalationStatus.Pending,
                Reason = "Lead Carlos Reyes not contacted for 7+ days. Requires manager attention."
            },
            new Escalation
            {
                TenantId = apex.Id, LeadId = apexLeads[2].Id,
                AssignedAgentId = apexBob.Id, EscalatedToId = apexManager.Id,
                Status = EscalationStatus.Acknowledged, AcknowledgedAt = DateTime.UtcNow.AddHours(-2),
                Reason = "James O'Brien follow-up was missed twice. High-priority lead."
            },
            new Escalation
            {
                TenantId = nova.Id, LeadId = novaLeads[2].Id,
                AssignedAgentId = novaRaj.Id, EscalatedToId = novaAdmin.Id,
                Status = EscalationStatus.Resolved, AcknowledgedAt = DateTime.UtcNow.AddDays(-1),
                ResolvedAt = DateTime.UtcNow.AddHours(-3),
                Reason = "Vikram Bose was marked Not Interested without manager review.",
                ResolutionNote = "Manager reviewed — confirmed correct outcome. Closing escalation."
            }
        );

        // ?? WEBHOOK CONFIGS ???????????????????????????????????????????????????
        db.WebhookConfigs.AddRange(
            new WebhookConfig
            {
                TenantId = apex.Id, Name = "Lead Converted — Slack Alert",
                Url = "https://hooks.slack.com/services/demo/apex/webhook",
                Secret = "whsec_apex_demo_secret",
                Events = System.Text.Json.JsonSerializer.Serialize(new[] { "LeadConverted", "PaymentReceived" }),
                IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-20)
            },
            new WebhookConfig
            {
                TenantId = apex.Id, Name = "CRM Sync — HubSpot",
                Url = "https://api.hubspot.com/crm/v3/demo/webhook",
                Events = System.Text.Json.JsonSerializer.Serialize(new[] { "LeadCreated", "LeadUpdated", "CallCompleted" }),
                IsActive = false, CreatedAt = DateTime.UtcNow.AddDays(-10)
            },
            new WebhookConfig
            {
                TenantId = nova.Id, Name = "New Subscriber Alert",
                Url = "https://novainternal.example.com/api/crm-hook",
                Events = System.Text.Json.JsonSerializer.Serialize(new[] { "LeadConverted" }),
                IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-5)
            }
        );

        // ?? NOTIFICATIONS ?????????????????????????????????????????????????????
        db.Notifications.AddRange(
            new Notification
            {
                TenantId = apex.Id, UserId = apexAlice.Id, Type = NotificationType.LeadConverted,
                Title = "Lead Converted!", Body = "Meera Iyer (Startup42) just converted. Deal closed!",
                Link = $"/Leads/Timeline/{apexLeads[4].Id}", IsRead = false
            },
            new Notification
            {
                TenantId = apex.Id, UserId = apexBob.Id, Type = NotificationType.FollowUpDue,
                Title = "Follow-up Due Soon",
                Body = "Follow-up with Anita Desai (CloudVision) via WhatsApp in 30 minutes.",
                Link = $"/Leads/Timeline/{apexLeads[6].Id}", IsRead = false
            },
            new Notification
            {
                TenantId = apex.Id, UserId = apexManager.Id, Type = NotificationType.SystemAlert,
                Title = "New Escalation: Missed Follow-ups",
                Body = "Carlos Reyes has 3+ missed follow-ups. Please review.",
                Link = $"/Leads/Timeline/{apexLeads[7].Id}", IsRead = false
            },
            new Notification
            {
                TenantId = nova.Id, UserId = novaRaj.Id, Type = NotificationType.NewLeadAssigned,
                Title = "New Lead Assigned", Body = "Sanjay Malhotra (TechPark) has been assigned to you.",
                Link = $"/Leads/Timeline/{novaLeads[4].Id}", IsRead = true
            }
        );

        // ?? ACTIVITY LOGS ?????????????????????????????????????????????????????
        db.ActivityLogs.AddRange(
            new ActivityLog { TenantId = apex.Id, LeadId = apexLeads[4].Id, UserId = apexAlice.Id, Type = ActivityType.LeadConverted, Summary = "Lead converted — annual plan purchased. Receipt RCPT-001 issued.", OccurredAt = DateTime.UtcNow.AddDays(-2) },
            new ActivityLog { TenantId = apex.Id, LeadId = apexLeads[4].Id, UserId = apexAlice.Id, Type = ActivityType.PaymentReceived, Summary = "Payment of USD 14,988 captured. Receipt RCPT-001.", OccurredAt = DateTime.UtcNow.AddDays(-2) },
            new ActivityLog { TenantId = apex.Id, LeadId = apexLeads[0].Id, UserId = apexAlice.Id, Type = ActivityType.MeetingScheduled, Summary = "Video call demo scheduled for 2 days from now.", OccurredAt = DateTime.UtcNow.AddDays(-1) },
            new ActivityLog { TenantId = apex.Id, LeadId = apexLeads[7].Id, UserId = apexBob.Id,   Type = ActivityType.EscalationRaised, Summary = "Escalation raised: no contact for 7+ days.", OccurredAt = DateTime.UtcNow.AddHours(-5) },
            new ActivityLog { TenantId = apex.Id, LeadId = apexLeads[12].Id, UserId = apexAlice.Id, Type = ActivityType.PaymentReceived, Summary = "Payment of USD 17,880 captured. Receipt RCPT-002.", OccurredAt = DateTime.UtcNow.AddDays(-5) },
            new ActivityLog { TenantId = nova.Id, LeadId = novaLeads[1].Id, UserId = novaRaj.Id,   Type = ActivityType.LeadConverted, Summary = "Deepa Nair signed annual Home Ultra plan.", OccurredAt = DateTime.UtcNow.AddDays(-1) }
        );

        await db.SaveChangesAsync();

        // ?? CALL SCRIPTS ??????????????????????????????????????????????????????
        var script1 = new CallScript
        {
            TenantId = apex.Id, CampaignId = c1.Id, IsActive = true,
            Title = "Enterprise Cold Call Script",
            Content = "Hi [Name], I'm [Agent] from Apex Sales. We help enterprise teams close 30% more deals with AI-assisted calling. Quick question — are your reps spending more time on admin than actually selling?\n\n[If YES] That's exactly what we solve. Can I show you a 5-minute demo?\n[If NO] Great! Out of curiosity, how are you currently tracking your lead pipeline?\n\nWrap-up: I'll send a quick overview and schedule a demo. What's the best time?",
            CreatedAt = DateTime.UtcNow.AddDays(-55), UpdatedAt = DateTime.UtcNow.AddDays(-10)
        };
        var script2 = new CallScript
        {
            TenantId = apex.Id, CampaignId = c2.Id, IsActive = true,
            Title = "SMB Upsell Script",
            Content = "Hi [Name], this is [Agent] from Apex. You've been on our starter plan for a while — I wanted to personally reach out.\n\nWe've just launched some powerful features on the premium plan, including AI lead scoring and automated follow-ups. I think it could save your team 2 hours every day.\n\nWould you be open to a quick 15-minute walkthrough?",
            CreatedAt = DateTime.UtcNow.AddDays(-18), UpdatedAt = DateTime.UtcNow.AddDays(-5)
        };
        var script3 = new CallScript
        {
            TenantId = nova.Id, CampaignId = c4.Id, IsActive = true,
            Title = "Broadband Intro Script",
            Content = "Hello [Name], I'm calling from Nova Telecom. We've just launched a blazing-fast 1 Gbps broadband plan in your area at just ?1499/month — that's half the price of most providers.\n\nAre you happy with your current internet speed?\n\n[If NO] Perfect — let me tell you about our Home Ultra plan.\n[If YES] That's great! With Nova you'd get the same speed plus unlimited data and free installation.",
            CreatedAt = DateTime.UtcNow.AddDays(-14), UpdatedAt = DateTime.UtcNow.AddDays(-2)
        };
        db.CallScripts.AddRange(script1, script2, script3);
        await db.SaveChangesAsync();

        // ?? DEAL PIPELINE ?????????????????????????????????????????????????????
        var deal1 = new Deal
        {
            TenantId = apex.Id, LeadId = apexLeads[0].Id, AssignedToId = apexAlice.Id,
            Title = "TechCorp India — Enterprise Licence",
            Value = 48000m, Currency = "USD", Stage = DealStage.Proposal, Probability = 60,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(15),
            Notes = "Rajesh is keen. Waiting for legal to review the MSA.",
            CreatedAt = DateTime.UtcNow.AddDays(-12), UpdatedAt = DateTime.UtcNow.AddDays(-2)
        };
        var deal2 = new Deal
        {
            TenantId = apex.Id, LeadId = apexLeads[4].Id, AssignedToId = apexAlice.Id,
            Title = "Startup42 — Annual Pro Plan",
            Value = 14988m, Currency = "USD", Stage = DealStage.ClosedWon, Probability = 100,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(-2),
            Notes = "Closed. Contract signed. Onboarding in progress.",
            CreatedAt = DateTime.UtcNow.AddDays(-20), UpdatedAt = DateTime.UtcNow.AddDays(-2)
        };
        var deal3 = new Deal
        {
            TenantId = apex.Id, LeadId = apexLeads[9].Id, AssignedToId = apexManager.Id,
            Title = "Tokyo Ventures — APAC Expansion Deal",
            Value = 120000m, Currency = "USD", Stage = DealStage.Negotiation, Probability = 45,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(30),
            Notes = "High value. CFO involved. Multiple stakeholders.",
            CreatedAt = DateTime.UtcNow.AddDays(-8), UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var deal4 = new Deal
        {
            TenantId = apex.Id, LeadId = apexLeads[6].Id, AssignedToId = apexBob.Id,
            Title = "CloudVision — Premium Upgrade",
            Value = 7200m, Currency = "USD", Stage = DealStage.Qualification, Probability = 30,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(20),
            Notes = "Demo done. Awaiting board approval.",
            CreatedAt = DateTime.UtcNow.AddDays(-5), UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var deal5 = new Deal
        {
            TenantId = apex.Id, LeadId = apexLeads[12].Id, AssignedToId = apexAlice.Id,
            Title = "SG Labs — 1 Year Enterprise",
            Value = 17880m, Currency = "USD", Stage = DealStage.ClosedWon, Probability = 100,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(-5),
            Notes = "Closed. 1-year contract. RCPT-002 issued.",
            CreatedAt = DateTime.UtcNow.AddDays(-15), UpdatedAt = DateTime.UtcNow.AddDays(-5)
        };
        var deal6 = new Deal
        {
            TenantId = apex.Id, LeadId = apexLeads[5].Id, AssignedToId = apexBob.Id,
            Title = "Fabricate GmbH — Starter Pilot",
            Value = 1788m, Currency = "EUR", Stage = DealStage.ClosedLost, Probability = 0,
            Notes = "Budget constraints. May revisit Q1 next year.",
            CreatedAt = DateTime.UtcNow.AddDays(-18), UpdatedAt = DateTime.UtcNow.AddDays(-3)
        };
        var deal7 = new Deal
        {
            TenantId = apex.Id, LeadId = apexLeads[2].Id, AssignedToId = apexBob.Id,
            Title = "Atlantic Digital — Starter Plan",
            Value = 1788m, Currency = "EUR", Stage = DealStage.Prospecting, Probability = 20,
            Notes = "Initial interest. Needs ROI calculation.",
            CreatedAt = DateTime.UtcNow.AddDays(-3), UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var deal8 = new Deal
        {
            TenantId = nova.Id, LeadId = novaLeads[0].Id, AssignedToId = novaRaj.Id,
            Title = "Arun Pillai — Home Ultra Annual",
            Value = 17988m, Currency = "INR", Stage = DealStage.Proposal, Probability = 65,
            Notes = "Very interested. Sending confirmation today.",
            CreatedAt = DateTime.UtcNow.AddDays(-4), UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var deal9 = new Deal
        {
            TenantId = nova.Id, LeadId = novaLeads[1].Id, AssignedToId = novaRaj.Id,
            Title = "Deepa Nair — Annual Home Ultra",
            Value = 17988m, Currency = "INR", Stage = DealStage.ClosedWon, Probability = 100,
            Notes = "Signed annual plan. Payment received.",
            CreatedAt = DateTime.UtcNow.AddDays(-10), UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.Deals.AddRange(deal1, deal2, deal3, deal4, deal5, deal6, deal7, deal8, deal9);
        await db.SaveChangesAsync();

        // ?? QUOTATION MANAGEMENT ??????????????????????????????????????????????
        var lineItems1 = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { description = "Enterprise Pro Licence (20 users)", qty = 1, unitPrice = 14988.0, amount = 14988.0 },
            new { description = "Dedicated Onboarding & Training", qty = 1, unitPrice = 2000.0, amount = 2000.0 }
        });
        var quote1 = new Quote
        {
            TenantId = apex.Id, LeadId = apexLeads[0].Id, DealId = deal1.Id, CreatedById = apexAlice.Id,
            QuoteNumber = "QT-APEX-001", Title = "Enterprise Licence — TechCorp India",
            Status = QuoteStatus.Sent, LineItemsJson = lineItems1,
            SubTotal = 16988m, DiscountAmount = 988m, TaxPercent = 18m,
            TaxAmount = 2880m, Total = 18880m, Currency = "USD",
            Notes = "10-day validity. GST included.", ExpiresAt = DateTime.UtcNow.AddDays(10),
            SentAt = DateTime.UtcNow.AddDays(-2),
            CreatedAt = DateTime.UtcNow.AddDays(-3), UpdatedAt = DateTime.UtcNow.AddDays(-2)
        };
        var lineItems2 = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { description = "Annual Pro Plan (5 users)", qty = 1, unitPrice = 14988.0, amount = 14988.0 }
        });
        var quote2 = new Quote
        {
            TenantId = apex.Id, LeadId = apexLeads[4].Id, DealId = deal2.Id, CreatedById = apexAlice.Id,
            QuoteNumber = "QT-APEX-002", Title = "Annual Pro Plan — Startup42",
            Status = QuoteStatus.Accepted, LineItemsJson = lineItems2,
            SubTotal = 14988m, DiscountAmount = 0m, TaxPercent = 18m,
            TaxAmount = 2697.84m, Total = 17685.84m, Currency = "USD",
            SentAt = DateTime.UtcNow.AddDays(-10), AcceptedAt = DateTime.UtcNow.AddDays(-2),
            CreatedAt = DateTime.UtcNow.AddDays(-12), UpdatedAt = DateTime.UtcNow.AddDays(-2)
        };
        var lineItems3 = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { description = "APAC Enterprise Licence (unlimited users)", qty = 1, unitPrice = 100000.0, amount = 100000.0 },
            new { description = "Premium Support SLA", qty = 1, unitPrice = 20000.0, amount = 20000.0 }
        });
        var quote3 = new Quote
        {
            TenantId = apex.Id, LeadId = apexLeads[9].Id, DealId = deal3.Id, CreatedById = apexManager.Id,
            QuoteNumber = "QT-APEX-003", Title = "APAC Expansion — Tokyo Ventures",
            Status = QuoteStatus.Draft, LineItemsJson = lineItems3,
            SubTotal = 120000m, DiscountAmount = 5000m, TaxPercent = 0m,
            TaxAmount = 0m, Total = 115000m, Currency = "USD",
            Notes = "Draft — pending legal review. Zero tax (export).",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow.AddDays(-1), UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var lineItems4 = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { description = "Nova Home Ultra — Annual (12 months)", qty = 1, unitPrice = 17988.0, amount = 17988.0 }
        });
        var quote4 = new Quote
        {
            TenantId = nova.Id, LeadId = novaLeads[0].Id, DealId = deal8.Id, CreatedById = novaRaj.Id,
            QuoteNumber = "QT-NOVA-001", Title = "Home Ultra Annual — Arun Pillai",
            Status = QuoteStatus.Sent, LineItemsJson = lineItems4,
            SubTotal = 17988m, DiscountAmount = 0m, TaxPercent = 18m,
            TaxAmount = 3237.84m, Total = 21225.84m, Currency = "INR",
            ExpiresAt = DateTime.UtcNow.AddDays(7), SentAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-2), UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.Quotes.AddRange(quote1, quote2, quote3, quote4);
        await db.SaveChangesAsync();

        // ?? INVOICES ??????????????????????????????????????????????????????????
        var payments = await db.Payments.Where(p => p.TenantId == apex.Id).ToListAsync();
        var inv1LineItems = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { description = "Annual Pro Plan", qty = 1, unitPrice = 14988.0, amount = 14988.0 }
        });
        db.Invoices.AddRange(
            new Invoice
            {
                TenantId = apex.Id, LeadId = apexLeads[4].Id, CreatedById = apexAlice.Id,
                InvoiceNumber = "INV-APEX-001", Status = InvoiceStatus.Paid,
                SubTotal = 14988m, TaxPercent = 18m, TaxAmount = 2697.84m, Total = 17685.84m,
                Currency = "USD", Description = "Annual Pro Plan — Startup42",
                LineItemsJson = inv1LineItems,
                IssuedAt = DateTime.UtcNow.AddDays(-3), DueAt = DateTime.UtcNow.AddDays(27),
                PaidAt = DateTime.UtcNow.AddDays(-2),
                CreatedAt = DateTime.UtcNow.AddDays(-3), UpdatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new Invoice
            {
                TenantId = apex.Id, LeadId = apexLeads[12].Id, CreatedById = apexAlice.Id,
                InvoiceNumber = "INV-APEX-002", Status = InvoiceStatus.Paid,
                SubTotal = 17880m, TaxPercent = 0m, TaxAmount = 0m, Total = 17880m,
                Currency = "USD", Description = "1-Year Enterprise Contract — SG Labs",
                LineItemsJson = System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new { description = "Enterprise Annual Licence", qty = 1, unitPrice = 17880.0, amount = 17880.0 }
                }),
                IssuedAt = DateTime.UtcNow.AddDays(-6), DueAt = DateTime.UtcNow.AddDays(24),
                PaidAt = DateTime.UtcNow.AddDays(-5),
                CreatedAt = DateTime.UtcNow.AddDays(-6), UpdatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new Invoice
            {
                TenantId = apex.Id, LeadId = apexLeads[2].Id, CreatedById = apexBob.Id,
                InvoiceNumber = "INV-APEX-003", Status = InvoiceStatus.Sent,
                SubTotal = 1788m, TaxPercent = 0m, TaxAmount = 0m, Total = 1788m,
                Currency = "EUR", Description = "Starter Plan — Atlantic Digital",
                LineItemsJson = System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new { description = "Starter Plan (Annual)", qty = 1, unitPrice = 1788.0, amount = 1788.0 }
                }),
                IssuedAt = DateTime.UtcNow.AddDays(-1), DueAt = DateTime.UtcNow.AddDays(29),
                CreatedAt = DateTime.UtcNow.AddDays(-1), UpdatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new Invoice
            {
                TenantId = nova.Id, LeadId = novaLeads[1].Id, CreatedById = novaRaj.Id,
                InvoiceNumber = "INV-NOVA-001", Status = InvoiceStatus.Paid,
                SubTotal = 17988m, TaxPercent = 18m, TaxAmount = 3237.84m, Total = 21225.84m,
                Currency = "INR", Description = "Annual Home Ultra Plan — Deepa Nair",
                LineItemsJson = System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new { description = "Home Ultra 1Gbps Annual Plan", qty = 1, unitPrice = 17988.0, amount = 17988.0 }
                }),
                IssuedAt = DateTime.UtcNow.AddDays(-2), DueAt = DateTime.UtcNow.AddDays(28),
                PaidAt = DateTime.UtcNow.AddDays(-1),
                CreatedAt = DateTime.UtcNow.AddDays(-2), UpdatedAt = DateTime.UtcNow.AddDays(-1)
            }
        );
        await db.SaveChangesAsync();

        // ?? COMMISSION TRACKER ????????????????????????????????????????????????
        var crule1 = new CommissionRule
        {
            TenantId = apex.Id, Name = "10% of Enterprise Deals", Type = CommissionType.PercentOfPayment,
            Value = 10m, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-60)
        };
        var crule2 = new CommissionRule
        {
            TenantId = apex.Id, Name = "Flat ?500 per Conversion", Type = CommissionType.FlatPerConversion,
            Value = 500m, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-60)
        };
        var crule3 = new CommissionRule
        {
            TenantId = nova.Id, Name = "5% of Annual Plan Value", Type = CommissionType.PercentOfPayment,
            Value = 5m, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-28)
        };
        db.CommissionRules.AddRange(crule1, crule2, crule3);
        await db.SaveChangesAsync();

        db.CommissionEntries.AddRange(
            new CommissionEntry
            {
                TenantId = apex.Id, AgentId = apexAlice.Id, RuleId = crule1.Id,
                LeadId = apexLeads[4].Id, Amount = 1498.8m,
                Status = CommissionStatus.Paid, EarnedAt = DateTime.UtcNow.AddDays(-2),
                PaidAt = DateTime.UtcNow.AddDays(-1), Note = "10% of $14,988 Annual Pro — Startup42"
            },
            new CommissionEntry
            {
                TenantId = apex.Id, AgentId = apexAlice.Id, RuleId = crule1.Id,
                LeadId = apexLeads[12].Id, Amount = 1788m,
                Status = CommissionStatus.Approved, EarnedAt = DateTime.UtcNow.AddDays(-5),
                Note = "10% of $17,880 Enterprise — SG Labs. Pending payout."
            },
            new CommissionEntry
            {
                TenantId = apex.Id, AgentId = apexBob.Id, RuleId = crule2.Id,
                LeadId = apexLeads[6].Id, Amount = 500m,
                Status = CommissionStatus.Pending, EarnedAt = DateTime.UtcNow.AddDays(-1),
                Note = "Flat bonus for qualified demo — CloudVision"
            },
            new CommissionEntry
            {
                TenantId = apex.Id, AgentId = apexBob.Id, RuleId = crule1.Id,
                LeadId = apexLeads[2].Id, Amount = 178.8m,
                Status = CommissionStatus.Pending, EarnedAt = DateTime.UtcNow,
                Note = "10% of €1,788 Starter — Atlantic Digital (pending payment)"
            },
            new CommissionEntry
            {
                TenantId = nova.Id, AgentId = novaRaj.Id, RuleId = crule3.Id,
                LeadId = novaLeads[1].Id, Amount = 899.4m,
                Status = CommissionStatus.Paid, EarnedAt = DateTime.UtcNow.AddDays(-1),
                PaidAt = DateTime.UtcNow, Note = "5% of ?17,988 Annual Ultra — Deepa Nair"
            }
        );
        await db.SaveChangesAsync();

        // ?? DRIP AUTOMATION ???????????????????????????????????????????????????
        var seq1 = new DripSequence
        {
            TenantId = apex.Id, CampaignId = c1.Id, Name = "Enterprise Lead Nurture",
            Trigger = AutomationTrigger.LeadCreated, IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-50)
        };
        var seq2 = new DripSequence
        {
            TenantId = apex.Id, CampaignId = c2.Id, Name = "Upsell Follow-up Sequence",
            Trigger = AutomationTrigger.CampaignEnrolled, IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-18)
        };
        var seq3 = new DripSequence
        {
            TenantId = apex.Id, Name = "Post-Demo Drip",
            Trigger = AutomationTrigger.LeadStatusChanged, IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };
        var seq4 = new DripSequence
        {
            TenantId = nova.Id, CampaignId = c4.Id, Name = "Broadband Interest Drip",
            Trigger = AutomationTrigger.LeadCreated, IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-14)
        };
        db.DripSequences.AddRange(seq1, seq2, seq3, seq4);
        await db.SaveChangesAsync();

        db.DripSteps.AddRange(
            // Seq 1 — Enterprise Lead Nurture
            new DripStep { SequenceId = seq1.Id, StepOrder = 1, StepType = AutomationStepType.SendEmail,    DelayDays = 0, Payload = "Welcome to Apex Sales! Here's your personalised overview.", CreatedAt = DateTime.UtcNow.AddDays(-50) },
            new DripStep { SequenceId = seq1.Id, StepOrder = 2, StepType = AutomationStepType.Wait,           DelayDays = 2, Payload = "", CreatedAt = DateTime.UtcNow.AddDays(-50) },
            new DripStep { SequenceId = seq1.Id, StepOrder = 3, StepType = AutomationStepType.SendWhatsApp,   DelayDays = 3, Payload = "Hi {{name}}, just checking in! Did you get a chance to review our overview?", CreatedAt = DateTime.UtcNow.AddDays(-50) },
            new DripStep { SequenceId = seq1.Id, StepOrder = 4, StepType = AutomationStepType.AssignAgent,    DelayDays = 5, Payload = "Escalate to senior agent if no response.", CreatedAt = DateTime.UtcNow.AddDays(-50) },
            new DripStep { SequenceId = seq1.Id, StepOrder = 5, StepType = AutomationStepType.SendSms,        DelayDays = 7, Payload = "Hi {{name}}, this is your last reminder! Reply YES to schedule a call.", CreatedAt = DateTime.UtcNow.AddDays(-50) },
            // Seq 2 — Upsell Follow-up
            new DripStep { SequenceId = seq2.Id, StepOrder = 1, StepType = AutomationStepType.SendEmail,    DelayDays = 0, Payload = "Exclusive: Unlock premium features with just a quick upgrade!", CreatedAt = DateTime.UtcNow.AddDays(-18) },
            new DripStep { SequenceId = seq2.Id, StepOrder = 2, StepType = AutomationStepType.Wait,           DelayDays = 3, Payload = "", CreatedAt = DateTime.UtcNow.AddDays(-18) },
            new DripStep { SequenceId = seq2.Id, StepOrder = 3, StepType = AutomationStepType.SendSms,        DelayDays = 4, Payload = "Hi {{name}}, your premium trial is waiting! Call us at 1800-APEX.", CreatedAt = DateTime.UtcNow.AddDays(-18) },
            new DripStep { SequenceId = seq2.Id, StepOrder = 4, StepType = AutomationStepType.AddTag,         DelayDays = 7, Payload = "upsell-drip-complete", CreatedAt = DateTime.UtcNow.AddDays(-18) },
            // Seq 3 — Post-Demo
            new DripStep { SequenceId = seq3.Id, StepOrder = 1, StepType = AutomationStepType.SendEmail,    DelayDays = 0, Payload = "Thanks for attending the demo! Here's the recording + proposal.", CreatedAt = DateTime.UtcNow.AddDays(-10) },
            new DripStep { SequenceId = seq3.Id, StepOrder = 2, StepType = AutomationStepType.SendWhatsApp,   DelayDays = 2, Payload = "Hi {{name}}, any questions about the demo? I'm here to help!", CreatedAt = DateTime.UtcNow.AddDays(-10) },
            new DripStep { SequenceId = seq3.Id, StepOrder = 3, StepType = AutomationStepType.UpdateStatus,   DelayDays = 5, Payload = "FollowUp", CreatedAt = DateTime.UtcNow.AddDays(-10) },
            // Seq 4 — Nova Broadband
            new DripStep { SequenceId = seq4.Id, StepOrder = 1, StepType = AutomationStepType.SendSms,        DelayDays = 0, Payload = "Hi {{name}}, thanks for your interest in Nova Telecom! Check out our 1Gbps plan.", CreatedAt = DateTime.UtcNow.AddDays(-14) },
            new DripStep { SequenceId = seq4.Id, StepOrder = 2, StepType = AutomationStepType.Wait,           DelayDays = 1, Payload = "", CreatedAt = DateTime.UtcNow.AddDays(-14) },
            new DripStep { SequenceId = seq4.Id, StepOrder = 3, StepType = AutomationStepType.SendWhatsApp,   DelayDays = 2, Payload = "Get Nova's 1Gbps plan at ?1499/month. Limited slots! Reply NOW.", CreatedAt = DateTime.UtcNow.AddDays(-14) }
        );

        // Drip Enrollments
        db.DripEnrollments.AddRange(
            new DripEnrollment { SequenceId = seq1.Id, LeadId = apexLeads[0].Id, TenantId = apex.Id, Status = EnrollmentStatus.Active, CurrentStep = 3, EnrolledAt = DateTime.UtcNow.AddDays(-8), NextRunAt = DateTime.UtcNow.AddDays(1) },
            new DripEnrollment { SequenceId = seq1.Id, LeadId = apexLeads[3].Id, TenantId = apex.Id, Status = EnrollmentStatus.Active, CurrentStep = 1, EnrolledAt = DateTime.UtcNow.AddDays(-2), NextRunAt = DateTime.UtcNow.AddDays(2) },
            new DripEnrollment { SequenceId = seq2.Id, LeadId = apexLeads[1].Id, TenantId = apex.Id, Status = EnrollmentStatus.Completed, CurrentStep = 4, EnrolledAt = DateTime.UtcNow.AddDays(-15), NextRunAt = null },
            new DripEnrollment { SequenceId = seq3.Id, LeadId = apexLeads[6].Id, TenantId = apex.Id, Status = EnrollmentStatus.Active, CurrentStep = 2, EnrolledAt = DateTime.UtcNow.AddDays(-3), NextRunAt = DateTime.UtcNow.AddDays(1) },
            new DripEnrollment { SequenceId = seq4.Id, LeadId = novaLeads[0].Id, TenantId = nova.Id, Status = EnrollmentStatus.Active, CurrentStep = 2, EnrolledAt = DateTime.UtcNow.AddDays(-3), NextRunAt = DateTime.UtcNow.AddDays(1) },
            new DripEnrollment { SequenceId = seq4.Id, LeadId = novaLeads[3].Id, TenantId = nova.Id, Status = EnrollmentStatus.Active, CurrentStep = 1, EnrolledAt = DateTime.UtcNow.AddDays(-1), NextRunAt = DateTime.UtcNow.AddDays(2) }
        );
        await db.SaveChangesAsync();

        // ?? POST-CALL DISPOSITION FORMS ???????????????????????????????????????
        var dispForm1 = new DispositionForm
        {
            TenantId = apex.Id, CampaignId = c1.Id, Name = "Enterprise Call Outcome",
            IsDefault = true, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-45)
        };
        var dispForm2 = new DispositionForm
        {
            TenantId = apex.Id, CampaignId = c2.Id, Name = "Upsell Call Feedback",
            IsDefault = false, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-15)
        };
        var dispForm3 = new DispositionForm
        {
            TenantId = nova.Id, CampaignId = c4.Id, Name = "Broadband Lead Disposition",
            IsDefault = true, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-12)
        };
        db.DispositionForms.AddRange(dispForm1, dispForm2, dispForm3);
        await db.SaveChangesAsync();

        var df1f1 = new DispositionField { FormId = dispForm1.Id, Label = "Call Outcome",     FieldType = DispositionFieldType.Select,   Options = "Interested,Not Interested,Callback,Converted,Wrong Number", IsRequired = true,  SortOrder = 1 };
        var df1f2 = new DispositionField { FormId = dispForm1.Id, Label = "Decision Maker Reached", FieldType = DispositionFieldType.Checkbox, IsRequired = false, SortOrder = 2 };
        var df1f3 = new DispositionField { FormId = dispForm1.Id, Label = "Next Step",        FieldType = DispositionFieldType.Text,     IsRequired = false, SortOrder = 3 };
        var df1f4 = new DispositionField { FormId = dispForm1.Id, Label = "Deal Size Estimate", FieldType = DispositionFieldType.Number, IsRequired = false, SortOrder = 4 };
        var df1f5 = new DispositionField { FormId = dispForm1.Id, Label = "Sentiment Rating", FieldType = DispositionFieldType.Rating,   IsRequired = true,  SortOrder = 5 };

        var df2f1 = new DispositionField { FormId = dispForm2.Id, Label = "Interested in Upgrade", FieldType = DispositionFieldType.Select, Options = "Yes,No,Maybe", IsRequired = true,  SortOrder = 1 };
        var df2f2 = new DispositionField { FormId = dispForm2.Id, Label = "Objection Raised", FieldType = DispositionFieldType.Text,   IsRequired = false, SortOrder = 2 };
        var df2f3 = new DispositionField { FormId = dispForm2.Id, Label = "Follow-up Date",  FieldType = DispositionFieldType.Date,   IsRequired = false, SortOrder = 3 };

        var df3f1 = new DispositionField { FormId = dispForm3.Id, Label = "Plan Interest",   FieldType = DispositionFieldType.Select, Options = "Home Basic,Home Plus,Home Ultra,Not Interested", IsRequired = true, SortOrder = 1 };
        var df3f2 = new DispositionField { FormId = dispForm3.Id, Label = "Installation Slot Confirmed", FieldType = DispositionFieldType.Checkbox, IsRequired = false, SortOrder = 2 };
        var df3f3 = new DispositionField { FormId = dispForm3.Id, Label = "Agent Notes",     FieldType = DispositionFieldType.Text,   IsRequired = false, SortOrder = 3 };
        var df3f4 = new DispositionField { FormId = dispForm3.Id, Label = "Customer Rating", FieldType = DispositionFieldType.Rating, IsRequired = false, SortOrder = 4 };

        db.DispositionFields.AddRange(df1f1, df1f2, df1f3, df1f4, df1f5, df2f1, df2f2, df2f3, df3f1, df3f2, df3f3, df3f4);
        await db.SaveChangesAsync();

        // Disposition Responses (linked to seeded calls)
        var seedCalls = await db.Calls.Where(c => c.TenantId == apex.Id).Take(4).ToListAsync();
        var novaCalls = await db.Calls.Where(c => c.TenantId == nova.Id).Take(2).ToListAsync();
        if (seedCalls.Count >= 4)
        {
            db.DispositionResponses.AddRange(
                new DispositionResponse
                {
                    TenantId = apex.Id, FormId = dispForm1.Id,
                    CallId = seedCalls[0].Id, AgentId = apexAlice.Id, LeadId = seedCalls[0].LeadId,
                    AnswersJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
                    {
                        { "Call Outcome", "Interested" }, { "Decision Maker Reached", "true" },
                        { "Next Step", "Schedule product demo" }, { "Deal Size Estimate", "48000" }, { "Sentiment Rating", "4" }
                    }),
                    SubmittedAt = DateTime.UtcNow.AddDays(-6)
                },
                new DispositionResponse
                {
                    TenantId = apex.Id, FormId = dispForm1.Id,
                    CallId = seedCalls[1].Id, AgentId = apexAlice.Id, LeadId = seedCalls[1].LeadId,
                    AnswersJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
                    {
                        { "Call Outcome", "Callback" }, { "Decision Maker Reached", "false" },
                        { "Next Step", "Call back Monday 3pm" }, { "Sentiment Rating", "3" }
                    }),
                    SubmittedAt = DateTime.UtcNow.AddDays(-5)
                },
                new DispositionResponse
                {
                    TenantId = apex.Id, FormId = dispForm2.Id,
                    CallId = seedCalls[2].Id, AgentId = apexBob.Id, LeadId = seedCalls[2].LeadId,
                    AnswersJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
                    {
                        { "Interested in Upgrade", "Maybe" }, { "Objection Raised", "Needs board approval" },
                        { "Follow-up Date", DateTime.UtcNow.AddDays(5).ToString("yyyy-MM-dd") }
                    }),
                    SubmittedAt = DateTime.UtcNow.AddDays(-3)
                },
                new DispositionResponse
                {
                    TenantId = apex.Id, FormId = dispForm1.Id,
                    CallId = seedCalls[3].Id, AgentId = apexBob.Id, LeadId = seedCalls[3].LeadId,
                    AnswersJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
                    {
                        { "Call Outcome", "Converted" }, { "Decision Maker Reached", "true" },
                        { "Next Step", "Send contract" }, { "Deal Size Estimate", "17880" }, { "Sentiment Rating", "5" }
                    }),
                    SubmittedAt = DateTime.UtcNow.AddDays(-2)
                }
            );
        }
        if (novaCalls.Count >= 2)
        {
            db.DispositionResponses.AddRange(
                new DispositionResponse
                {
                    TenantId = nova.Id, FormId = dispForm3.Id,
                    CallId = novaCalls[0].Id, AgentId = novaRaj.Id, LeadId = novaCalls[0].LeadId,
                    AnswersJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
                    {
                        { "Plan Interest", "Home Ultra" }, { "Installation Slot Confirmed", "true" },
                        { "Agent Notes", "Customer very excited. Installation tomorrow." }, { "Customer Rating", "5" }
                    }),
                    SubmittedAt = DateTime.UtcNow.AddDays(-2)
                },
                new DispositionResponse
                {
                    TenantId = nova.Id, FormId = dispForm3.Id,
                    CallId = novaCalls[1].Id, AgentId = novaRaj.Id, LeadId = novaCalls[1].LeadId,
                    AnswersJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
                    {
                        { "Plan Interest", "Not Interested" }, { "Installation Slot Confirmed", "false" },
                        { "Agent Notes", "Already on competitor. Will follow up in 3 months." }, { "Customer Rating", "2" }
                    }),
                    SubmittedAt = DateTime.UtcNow.AddDays(-1)
                }
            );
        }
        await db.SaveChangesAsync();

        // ?? NPS SURVEYS ???????????????????????????????????????????????????????
        var survey1 = new NpsSurvey
        {
            TenantId = apex.Id, CampaignId = c1.Id, Name = "Post-Call NPS — Enterprise",
            IntroText = "On a scale of 0–10, how likely are you to recommend Apex Sales to a colleague?",
            Trigger = SurveyTrigger.AfterCall, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-40)
        };
        var survey2 = new NpsSurvey
        {
            TenantId = apex.Id, Name = "Post-Conversion Satisfaction",
            IntroText = "You recently converted to our platform. How would you rate your experience (0–10)?",
            Trigger = SurveyTrigger.AfterConversion, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-20)
        };
        var survey3 = new NpsSurvey
        {
            TenantId = nova.Id, CampaignId = c4.Id, Name = "Broadband Signup NPS",
            IntroText = "How likely are you to recommend Nova Telecom to friends or family?",
            Trigger = SurveyTrigger.AfterConversion, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-12)
        };
        db.NpsSurveys.AddRange(survey1, survey2, survey3);
        await db.SaveChangesAsync();

        var npsCallsApex = await db.Calls.Where(c => c.TenantId == apex.Id).Take(6).ToListAsync();
        var npsCallsNova = await db.Calls.Where(c => c.TenantId == nova.Id).Take(3).ToListAsync();

        var npsResponses = new List<NpsSurveyResponse>();
        var npsScores = new[] { 9, 8, 10, 7, 6, 9 };
        var npsFeedback = new[]
        {
            "Really smooth process. Agent was very helpful.", "Good experience overall.",
            "Excellent! Closed the deal quickly.", "Took a while but resolved.",
            "A bit pushy but knowledgeable.", "Very professional agent."
        };
        for (int i = 0; i < Math.Min(npsCallsApex.Count, 6); i++)
        {
            var c = npsCallsApex[i];
            npsResponses.Add(new NpsSurveyResponse
            {
                SurveyId = i < 3 ? survey1.Id : survey2.Id,
                LeadId = c.LeadId, AgentId = c.AgentId, CallId = c.Id,
                Score = npsScores[i], Feedback = npsFeedback[i],
                RespondedAt = c.StartedAt.AddHours(2)
            });
        }
        var novaScores = new[] { 10, 9, 4 };
        var novaFeedback = new[] { "Super fast and friendly!", "Great value for money.", "Sales call was too long." };
        for (int i = 0; i < Math.Min(npsCallsNova.Count, 3); i++)
        {
            var c = npsCallsNova[i];
            npsResponses.Add(new NpsSurveyResponse
            {
                SurveyId = survey3.Id,
                LeadId = c.LeadId, AgentId = c.AgentId, CallId = c.Id,
                Score = novaScores[i], Feedback = novaFeedback[i],
                RespondedAt = c.StartedAt.AddHours(1)
            });
        }
        db.NpsSurveyResponses.AddRange(npsResponses);
        await db.SaveChangesAsync();

        // ?? CALENDAR SYNC ?????????????????????????????????????????????????????
        db.CalendarSyncConfigs.AddRange(
            new CalendarSyncConfig
            {
                UserId = apexAlice.Id, Provider = CalendarProvider.Google,
                Status = CalendarSyncStatus.Connected,
                AccessToken = "demo_google_access_token_alice",
                RefreshToken = "demo_google_refresh_token_alice",
                TokenExpiresAt = DateTime.UtcNow.AddDays(30),
                CalendarId = "alice@apexsales.com",
                SyncFollowUps = true, SyncMeetings = true,
                CreatedAt = DateTime.UtcNow.AddDays(-10), UpdatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new CalendarSyncConfig
            {
                UserId = apexManager.Id, Provider = CalendarProvider.Outlook,
                Status = CalendarSyncStatus.Connected,
                AccessToken = "demo_outlook_access_token_manager",
                RefreshToken = "demo_outlook_refresh_token_manager",
                TokenExpiresAt = DateTime.UtcNow.AddDays(60),
                CalendarId = "manager@apexsales.com",
                SyncFollowUps = true, SyncMeetings = true,
                CreatedAt = DateTime.UtcNow.AddDays(-7), UpdatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new CalendarSyncConfig
            {
                UserId = novaRaj.Id, Provider = CalendarProvider.Google,
                Status = CalendarSyncStatus.Connected,
                AccessToken = "demo_google_access_token_raj",
                RefreshToken = "demo_google_refresh_token_raj",
                TokenExpiresAt = DateTime.UtcNow.AddDays(45),
                CalendarId = "raj@novatelecom.com",
                SyncFollowUps = true, SyncMeetings = false,
                CreatedAt = DateTime.UtcNow.AddDays(-5), UpdatedAt = DateTime.UtcNow
            }
        );

        // ?? AGENT SHIFTS ??????????????????????????????????????????????????????
        db.AgentShifts.AddRange(
            new AgentShift
            {
                TenantId = apex.Id, AgentId = apexAlice.Id,
                ShiftStartUtc = new TimeSpan(3, 30, 0), ShiftEndUtc = new TimeSpan(12, 30, 0),
                WorkDays = 62, Timezone = "Asia/Kolkata", IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30), UpdatedAt = DateTime.UtcNow.AddDays(-30)
            },
            new AgentShift
            {
                TenantId = apex.Id, AgentId = apexBob.Id,
                ShiftStartUtc = new TimeSpan(4, 0, 0), ShiftEndUtc = new TimeSpan(13, 0, 0),
                WorkDays = 62, Timezone = "Asia/Kolkata", IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30), UpdatedAt = DateTime.UtcNow.AddDays(-30)
            },
            new AgentShift
            {
                TenantId = nova.Id, AgentId = novaRaj.Id,
                ShiftStartUtc = new TimeSpan(3, 30, 0), ShiftEndUtc = new TimeSpan(12, 30, 0),
                WorkDays = 126, Timezone = "Asia/Kolkata", IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-15), UpdatedAt = DateTime.UtcNow.AddDays(-15)
            }
        );

        // ?? API KEYS ??????????????????????????????????????????????????????????
        db.ApiKeys.AddRange(
            new ApiKey
            {
                TenantId = apex.Id, CreatedById = apexAdmin.Id,
                Name = "HubSpot Integration Key", KeyPrefix = "ak_apex",
                KeyHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("demo-apex-api-key-001"))),
                Scopes = "leads:read leads:write calls:read", IsActive = true,
                LastUsedAt = DateTime.UtcNow.AddHours(-2),
                CreatedAt = DateTime.UtcNow.AddDays(-20)
            },
            new ApiKey
            {
                TenantId = nova.Id, CreatedById = novaAdmin.Id,
                Name = "Internal CRM Webhook Key", KeyPrefix = "ak_nova",
                KeyHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("demo-nova-api-key-001"))),
                Scopes = "leads:read calls:read payments:read", IsActive = true,
                LastUsedAt = DateTime.UtcNow.AddDays(-1),
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            }
        );

        // ?? CUSTOM LEAD FIELDS ????????????????????????????????????????????????
        db.CustomLeadFields.AddRange(
            new CustomLeadField { TenantId = apex.Id, Name = "company_size", Label = "Company Size", FieldType = "select", Options = "1-10,11-50,51-200,201-1000,1000+", IsRequired = false, IsActive = true, SortOrder = 1 },
            new CustomLeadField { TenantId = apex.Id, Name = "budget_usd", Label = "Annual Budget (USD)", FieldType = "number", IsRequired = false, IsActive = true, SortOrder = 2 },
            new CustomLeadField { TenantId = apex.Id, Name = "decision_timeline", Label = "Decision Timeline", FieldType = "select", Options = "Immediate,1-3 months,3-6 months,6+ months", IsRequired = false, IsActive = true, SortOrder = 3 },
            new CustomLeadField { TenantId = nova.Id, Name = "current_provider", Label = "Current ISP", FieldType = "text", IsRequired = false, IsActive = true, SortOrder = 1 },
            new CustomLeadField { TenantId = nova.Id, Name = "current_speed", Label = "Current Speed (Mbps)", FieldType = "number", IsRequired = false, IsActive = true, SortOrder = 2 }
        );

        // ?? LEAD TAGS ?????????????????????????????????????????????????????????
        db.LeadTags.AddRange(
            new LeadTag { TenantId = apex.Id, Name = "hot-lead",       Color = "#ef4444" },
            new LeadTag { TenantId = apex.Id, Name = "enterprise",     Color = "#8b5cf6" },
            new LeadTag { TenantId = apex.Id, Name = "demo-done",      Color = "#3b82f6" },
            new LeadTag { TenantId = apex.Id, Name = "upsell-target",  Color = "#f59e0b" },
            new LeadTag { TenantId = apex.Id, Name = "upsell-drip-complete", Color = "#10b981" },
            new LeadTag { TenantId = nova.Id, Name = "fiber-ready",    Color = "#06b6d4" },
            new LeadTag { TenantId = nova.Id, Name = "annual-plan",    Color = "#22c55e" }
        );

        // ?? CRM SYNC CONFIGS ??????????????????????????????????????????????????
        db.CrmSyncConfigs.AddRange(
            new CrmSyncConfig
            {
                TenantId = apex.Id, Provider = "hubspot", IsActive = true,
                PortalId = "demo-hubspot-portal-123",
                AccessToken = "demo_hubspot_access_token",
                RefreshToken = "demo_hubspot_refresh_token",
                TokenExpiresAt = DateTime.UtcNow.AddDays(30),
                LastSyncedAt = DateTime.UtcNow.AddHours(-6),
                LastSyncStatus = "Success — 12 leads synced.",
                CreatedAt = DateTime.UtcNow.AddDays(-25), UpdatedAt = DateTime.UtcNow.AddHours(-6)
            }
        );

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
