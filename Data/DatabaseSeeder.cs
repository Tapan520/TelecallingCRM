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
