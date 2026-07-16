using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Data;

public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Call> Calls => Set<Call>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<LeadDocument> LeadDocuments => Set<LeadDocument>();
    public DbSet<WhatsAppMessage> WhatsAppMessages => Set<WhatsAppMessage>();
    public DbSet<SmsMessage> SmsMessages => Set<SmsMessage>();
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<WebhookConfig> WebhookConfigs => Set<WebhookConfig>();
    public DbSet<WebhookDeliveryLog> WebhookDeliveryLogs => Set<WebhookDeliveryLog>();
    public DbSet<IntegrationConfig> IntegrationConfigs => Set<IntegrationConfig>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<MeetingAttendee> MeetingAttendees => Set<MeetingAttendee>();
    public DbSet<Escalation> Escalations => Set<Escalation>();
    public DbSet<EscalationRule> EscalationRules => Set<EscalationRule>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CallControlEvent> CallControlEvents => Set<CallControlEvent>();
    public DbSet<DncEntry> DncEntries => Set<DncEntry>();
    public DbSet<SmsTemplate> SmsTemplates => Set<SmsTemplate>();
    public DbSet<WhatsAppTemplate> WhatsAppTemplates => Set<WhatsAppTemplate>();
    public DbSet<AgentGoal> AgentGoals => Set<AgentGoal>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<CustomLeadField> CustomLeadFields => Set<CustomLeadField>();
    public DbSet<LeadTag> LeadTags => Set<LeadTag>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<AgentShift> AgentShifts => Set<AgentShift>();
    public DbSet<AgentPresence> AgentPresences => Set<AgentPresence>();
    public DbSet<RoundRobinState> RoundRobinStates => Set<RoundRobinState>();
    public DbSet<CallScript> CallScripts => Set<CallScript>();
    public DbSet<CallDisposition> CallDispositions => Set<CallDisposition>();
    public DbSet<CrmSyncConfig> CrmSyncConfigs => Set<CrmSyncConfig>();
    public DbSet<CrmSyncLog> CrmSyncLogs => Set<CrmSyncLog>();
    public DbSet<Invoice> Invoices => Set<Invoice>();

    // New modules
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<DripSequence> DripSequences => Set<DripSequence>();
    public DbSet<DripStep> DripSteps => Set<DripStep>();
    public DbSet<DripEnrollment> DripEnrollments => Set<DripEnrollment>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<CommissionRule> CommissionRules => Set<CommissionRule>();
    public DbSet<CommissionEntry> CommissionEntries => Set<CommissionEntry>();
    public DbSet<DispositionForm> DispositionForms => Set<DispositionForm>();
    public DbSet<DispositionField> DispositionFields => Set<DispositionField>();
    public DbSet<DispositionResponse> DispositionResponses => Set<DispositionResponse>();
    public DbSet<NpsSurvey> NpsSurveys => Set<NpsSurvey>();
    public DbSet<NpsSurveyResponse> NpsSurveyResponses => Set<NpsSurveyResponse>();
    public DbSet<CalendarSyncConfig> CalendarSyncConfigs => Set<CalendarSyncConfig>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(e => e.HasIndex(t => t.Slug).IsUnique());

        builder.Entity<AppUser>(e =>
        {
            e.HasOne(u => u.Tenant).WithMany(t => t.Users)
             .HasForeignKey(u => u.TenantId).IsRequired(false).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Lead>(e =>
        {
            e.HasOne(l => l.Tenant).WithMany(t => t.Leads)
             .HasForeignKey(l => l.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.AssignedTo).WithMany(u => u.AssignedLeads)
             .HasForeignKey(l => l.AssignedToId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.Campaign).WithMany(c => c.Leads)
             .HasForeignKey(l => l.CampaignId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(l => l.TenantId);
            e.HasIndex(l => new { l.TenantId, l.Status });
        });

        builder.Entity<Campaign>(e =>
        {
            e.HasOne(c => c.Tenant).WithMany(t => t.Campaigns)
             .HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => c.TenantId);
        });

        builder.Entity<Call>(e =>
        {
            e.HasOne(c => c.Lead).WithMany(l => l.Calls)
             .HasForeignKey(c => c.LeadId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Agent).WithMany(u => u.Calls)
             .HasForeignKey(c => c.AgentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(c => c.TenantId);
            e.HasIndex(c => c.AgentId);
        });

        builder.Entity<KnowledgeChunk>(e =>
        {
            e.HasOne(k => k.Tenant).WithMany(t => t.KnowledgeChunks)
             .HasForeignKey(k => k.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.Property(k => k.EmbeddingJson).HasColumnType("LONGTEXT");
            e.HasIndex(k => k.TenantId);
        });

        builder.Entity<FollowUp>(e =>
        {
            e.HasOne(f => f.Tenant).WithMany(t => t.FollowUps)
             .HasForeignKey(f => f.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(f => f.Lead).WithMany(l => l.FollowUps)
             .HasForeignKey(f => f.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.AssignedTo).WithMany(u => u.FollowUps)
             .HasForeignKey(f => f.AssignedToId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(f => new { f.TenantId, f.Status });
            e.HasIndex(f => new { f.AssignedToId, f.ScheduledAt });
        });

        builder.Entity<TaskItem>(e =>
        {
            e.HasOne(t => t.Tenant).WithMany(tn => tn.Tasks)
             .HasForeignKey(t => t.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.Lead).WithMany(l => l.Tasks)
             .HasForeignKey(t => t.LeadId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(t => t.AssignedTo).WithMany(u => u.AssignedTasks)
             .HasForeignKey(t => t.AssignedToId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.CreatedBy).WithMany(u => u.CreatedTasks)
             .HasForeignKey(t => t.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(t => new { t.TenantId, t.Status });
            e.HasIndex(t => t.AssignedToId);
        });

        builder.Entity<ActivityLog>(e =>
        {
            e.HasOne(a => a.Tenant).WithMany(t => t.ActivityLogs)
             .HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Lead).WithMany(l => l.ActivityLogs)
             .HasForeignKey(a => a.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.User).WithMany(u => u.ActivityLogs)
             .HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(a => new { a.LeadId, a.OccurredAt });
        });

        builder.Entity<Notification>(e =>
        {
            e.HasOne(n => n.Tenant).WithMany(t => t.Notifications)
             .HasForeignKey(n => n.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(n => n.User).WithMany(u => u.Notifications)
             .HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(n => new { n.UserId, n.IsRead });
        });

        builder.Entity<LeadDocument>(e =>
        {
            e.HasOne(d => d.Tenant).WithMany(t => t.Documents)
             .HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.Lead).WithMany(l => l.Documents)
             .HasForeignKey(d => d.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.UploadedBy).WithMany(u => u.UploadedDocuments)
             .HasForeignKey(d => d.UploadedById).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(d => d.LeadId);
        });

        builder.Entity<WhatsAppMessage>(e =>
        {
            e.HasOne(w => w.Tenant).WithMany(t => t.WhatsAppMessages)
             .HasForeignKey(w => w.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(w => w.Lead).WithMany(l => l.WhatsAppMessages)
             .HasForeignKey(w => w.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(w => w.SentBy).WithMany(u => u.WhatsAppMessages)
             .HasForeignKey(w => w.SentById).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(w => w.LeadId);
            e.HasIndex(w => w.TenantId);
        });

        builder.Entity<SmsMessage>(e =>
        {
            e.HasOne(s => s.Tenant).WithMany(t => t.SmsMessages)
             .HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Lead).WithMany(l => l.SmsMessages)
             .HasForeignKey(s => s.LeadId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(s => s.SentBy).WithMany(u => u.SmsMessages)
             .HasForeignKey(s => s.SentById).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(s => s.TenantId);
        });

        builder.Entity<EmailMessage>(e =>
        {
            e.HasOne(em => em.Tenant).WithMany(t => t.EmailMessages)
             .HasForeignKey(em => em.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(em => em.Lead).WithMany(l => l.EmailMessages)
             .HasForeignKey(em => em.LeadId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(em => em.SentBy).WithMany(u => u.EmailMessages)
             .HasForeignKey(em => em.SentById).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(em => em.TenantId);
            e.HasIndex(em => em.TrackingToken);
        });

        builder.Entity<EmailTemplate>(e =>
        {
            e.HasOne(t => t.Tenant).WithMany(tn => tn.EmailTemplates)
             .HasForeignKey(t => t.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(t => t.TenantId);
        });

        builder.Entity<WebhookConfig>(e =>
        {
            e.HasOne(w => w.Tenant).WithMany(t => t.Webhooks)
             .HasForeignKey(w => w.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(w => w.TenantId);
        });

        builder.Entity<WebhookDeliveryLog>(e =>
        {
            e.HasOne(d => d.Webhook).WithMany(w => w.DeliveryLogs)
             .HasForeignKey(d => d.WebhookId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(d => d.WebhookId);
        });

        builder.Entity<TaskComment>(e =>
        {
            e.HasOne(c => c.Task).WithMany(t => t.Comments)
             .HasForeignKey(c => c.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.User).WithMany()
             .HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(c => c.TaskId);
        });

        builder.Entity<IntegrationConfig>(e =>
        {
            e.HasOne(i => i.Tenant).WithMany(t => t.Integrations)
             .HasForeignKey(i => i.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(i => new { i.TenantId, i.Provider }).IsUnique();
            e.Property(i => i.ConfigJson).HasColumnType("LONGTEXT");
        });

        builder.Entity<Meeting>(e =>
        {
            e.HasOne(m => m.Tenant).WithMany(t => t.Meetings)
             .HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Lead).WithMany(l => l.Meetings)
             .HasForeignKey(m => m.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.OrganisedBy).WithMany(u => u.OrganisedMeetings)
             .HasForeignKey(m => m.OrganisedById).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(m => new { m.TenantId, m.ScheduledAt });
            e.HasIndex(m => m.LeadId);
        });

        builder.Entity<MeetingAttendee>(e =>
        {
            e.HasOne(a => a.Meeting).WithMany(m => m.Attendees)
             .HasForeignKey(a => a.MeetingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.User).WithMany(u => u.MeetingAttendances)
             .HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(a => a.MeetingId);
        });

        builder.Entity<EscalationRule>(e =>
        {
            e.HasOne(r => r.Tenant).WithMany(t => t.EscalationRules)
             .HasForeignKey(r => r.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.EscalateTo).WithMany(u => u.EscalationRules)
             .HasForeignKey(r => r.EscalateToId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => r.TenantId);
        });

        builder.Entity<Escalation>(e =>
        {
            e.HasOne(es => es.Tenant).WithMany(t => t.Escalations)
             .HasForeignKey(es => es.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(es => es.Lead).WithMany(l => l.Escalations)
             .HasForeignKey(es => es.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(es => es.AssignedAgent).WithMany(u => u.EscalationsAssigned)
             .HasForeignKey(es => es.AssignedAgentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(es => es.EscalatedTo).WithMany(u => u.EscalationsReceived)
             .HasForeignKey(es => es.EscalatedToId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(es => es.Rule).WithMany(r => r.Escalations)
             .HasForeignKey(es => es.RuleId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(es => new { es.TenantId, es.Status });
            e.HasIndex(es => es.LeadId);
        });

        builder.Entity<Payment>(e =>
        {
            e.HasOne(p => p.Tenant).WithMany(t => t.Payments)
             .HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Lead).WithMany(l => l.Payments)
             .HasForeignKey(p => p.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.RecordedBy).WithMany(u => u.RecordedPayments)
             .HasForeignKey(p => p.RecordedById).OnDelete(DeleteBehavior.Restrict);
            e.Property(p => p.Amount).HasColumnType("decimal(18,2)");
            e.HasIndex(p => p.TenantId);
            e.HasIndex(p => p.LeadId);
            e.HasIndex(p => p.RazorpayOrderId);
        });

        builder.Entity<CallControlEvent>(e =>
        {
            e.HasOne(c => c.Call).WithMany(ca => ca.ControlEvents)
             .HasForeignKey(c => c.CallId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Agent).WithMany(u => u.CallControlEvents)
             .HasForeignKey(c => c.AgentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(c => c.CallId);
        });

        builder.Entity<DncEntry>(e =>
        {
            e.HasOne(d => d.Tenant).WithMany(t => t.DncEntries)
             .HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.AddedBy).WithMany(u => u.DncEntries)
             .HasForeignKey(d => d.AddedById).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(d => new { d.TenantId, d.Phone }).IsUnique();
        });

        builder.Entity<SmsTemplate>(e =>
        {
            e.HasOne(t => t.Tenant).WithMany(tn => tn.SmsTemplates)
             .HasForeignKey(t => t.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(t => t.TenantId);
        });

        builder.Entity<WhatsAppTemplate>(e =>
        {
            e.HasOne(t => t.Tenant).WithMany(tn => tn.WhatsAppTemplates)
             .HasForeignKey(t => t.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(t => t.TenantId);
        });

        builder.Entity<AgentGoal>(e =>
        {
            e.HasOne(g => g.Tenant).WithMany(t => t.AgentGoals)
             .HasForeignKey(g => g.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(g => g.Agent).WithMany(u => u.AgentGoals)
             .HasForeignKey(g => g.AgentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(g => g.CreatedBy).WithMany(u => u.CreatedGoals)
             .HasForeignKey(g => g.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(g => new { g.TenantId, g.AgentId });
        });

        builder.Entity<ApiKey>(e =>
        {
            e.HasOne(a => a.Tenant).WithMany()
             .HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.CreatedBy).WithMany()
             .HasForeignKey(a => a.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(a => a.TenantId);
        });

        builder.Entity<CustomLeadField>(e =>
        {
            e.HasOne(f => f.Tenant).WithMany()
             .HasForeignKey(f => f.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(f => f.TenantId);
        });

        builder.Entity<LeadTag>(e =>
        {
            e.HasOne(t => t.Tenant).WithMany()
             .HasForeignKey(t => t.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(t => new { t.TenantId, t.Name }).IsUnique();
        });

        builder.Entity<NotificationPreference>(e =>
        {
            e.HasOne(p => p.User).WithMany()
             .HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(p => new { p.UserId, p.NotificationType }).IsUnique();
        });

        builder.Entity<AgentShift>(e =>
        {
            e.HasOne(s => s.Tenant).WithMany(t => t.AgentShifts)
             .HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Agent).WithMany(u => u.AgentShifts)
             .HasForeignKey(s => s.AgentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(s => new { s.TenantId, s.AgentId });
        });

        builder.Entity<AgentPresence>(e =>
        {
            e.HasOne(p => p.Tenant).WithMany(t => t.AgentPresences)
             .HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Agent).WithMany(u => u.AgentPresences)
             .HasForeignKey(p => p.AgentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(p => new { p.AgentId, p.ChangedAt });
        });

        builder.Entity<RoundRobinState>(e =>
        {
            e.HasOne(r => r.Tenant).WithMany(t => t.RoundRobinStates)
             .HasForeignKey(r => r.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.Property(r => r.AgentQueueJson).HasColumnType("LONGTEXT");
            e.HasIndex(r => new { r.TenantId, r.CampaignId }).IsUnique();
        });

        builder.Entity<CallScript>(e =>
        {
            e.HasOne(s => s.Tenant).WithMany(t => t.CallScripts)
             .HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Campaign).WithMany()
             .HasForeignKey(s => s.CampaignId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.Property(s => s.Content).HasColumnType("LONGTEXT");
            e.HasIndex(s => s.TenantId);
        });

        builder.Entity<CallDisposition>(e =>
        {
            e.HasOne(d => d.Tenant).WithMany(t => t.CallDispositions)
             .HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.Script).WithMany(s => s.Dispositions)
             .HasForeignKey(d => d.ScriptId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(d => d.TenantId);
        });

        builder.Entity<CrmSyncConfig>(e =>
        {
            e.HasOne(c => c.Tenant).WithMany(t => t.CrmSyncConfigs)
             .HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => new { c.TenantId, c.Provider }).IsUnique();
        });

        builder.Entity<CrmSyncLog>(e =>
        {
            e.HasOne(l => l.Config).WithMany()
             .HasForeignKey(l => l.CrmSyncConfigId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(l => new { l.CrmSyncConfigId, l.SyncedAt });
        });

        builder.Entity<Invoice>(e =>
        {
            e.HasOne(i => i.Tenant).WithMany(t => t.Invoices)
             .HasForeignKey(i => i.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Lead).WithMany()
             .HasForeignKey(i => i.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.Payment).WithMany()
             .HasForeignKey(i => i.PaymentId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(i => i.CreatedBy).WithMany(u => u.CreatedInvoices)
             .HasForeignKey(i => i.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.Property(i => i.SubTotal).HasColumnType("decimal(18,2)");
            e.Property(i => i.TaxAmount).HasColumnType("decimal(18,2)");
            e.Property(i => i.Total).HasColumnType("decimal(18,2)");
            e.Property(i => i.TaxPercent).HasColumnType("decimal(5,2)");
            e.Property(i => i.LineItemsJson).HasColumnType("LONGTEXT");
            e.HasIndex(i => i.TenantId);
            e.HasIndex(i => new { i.TenantId, i.InvoiceNumber }).IsUnique();
        });

        // ?? Deal Pipeline ?????????????????????????????????????????????????????
        builder.Entity<Deal>(e =>
        {
            e.HasOne(d => d.Tenant).WithMany(t => t.Deals)
             .HasForeignKey(d => d.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.Lead).WithMany()
             .HasForeignKey(d => d.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.AssignedTo).WithMany()
             .HasForeignKey(d => d.AssignedToId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            e.Property(d => d.Value).HasColumnType("decimal(18,2)");
            e.HasIndex(d => d.TenantId);
            e.HasIndex(d => d.LeadId);
        });

        // ?? Drip Automation ???????????????????????????????????????????????????
        builder.Entity<DripSequence>(e =>
        {
            e.HasOne(s => s.Tenant).WithMany(t => t.DripSequences)
             .HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Campaign).WithMany()
             .HasForeignKey(s => s.CampaignId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(s => s.TenantId);
        });

        builder.Entity<DripStep>(e =>
        {
            e.HasOne(s => s.Sequence).WithMany(seq => seq.Steps)
             .HasForeignKey(s => s.SequenceId).OnDelete(DeleteBehavior.Cascade);
            e.Property(s => s.Payload).HasColumnType("LONGTEXT");
            e.HasIndex(s => s.SequenceId);
        });

        builder.Entity<DripEnrollment>(e =>
        {
            e.HasOne(en => en.Sequence).WithMany(s => s.Enrollments)
             .HasForeignKey(en => en.SequenceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(en => en.Lead).WithMany()
             .HasForeignKey(en => en.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(en => new { en.SequenceId, en.LeadId }).IsUnique();
            e.HasIndex(en => en.NextRunAt);
        });

        // ?? Quotation Management ??????????????????????????????????????????????
        builder.Entity<Quote>(e =>
        {
            e.HasOne(q => q.Tenant).WithMany(t => t.Quotes)
             .HasForeignKey(q => q.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(q => q.Lead).WithMany()
             .HasForeignKey(q => q.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(q => q.Deal).WithMany()
             .HasForeignKey(q => q.DealId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(q => q.CreatedBy).WithMany()
             .HasForeignKey(q => q.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.Property(q => q.SubTotal).HasColumnType("decimal(18,2)");
            e.Property(q => q.DiscountAmount).HasColumnType("decimal(18,2)");
            e.Property(q => q.TaxPercent).HasColumnType("decimal(5,2)");
            e.Property(q => q.TaxAmount).HasColumnType("decimal(18,2)");
            e.Property(q => q.Total).HasColumnType("decimal(18,2)");
            e.Property(q => q.LineItemsJson).HasColumnType("LONGTEXT");
            e.HasIndex(q => q.TenantId);
            e.HasIndex(q => new { q.TenantId, q.QuoteNumber }).IsUnique();
        });

        // ?? Commission Tracker ????????????????????????????????????????????????
        builder.Entity<CommissionRule>(e =>
        {
            e.HasOne(r => r.Tenant).WithMany(t => t.CommissionRules)
             .HasForeignKey(r => r.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Campaign).WithMany()
             .HasForeignKey(r => r.CampaignId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.Property(r => r.Value).HasColumnType("decimal(10,2)");
            e.HasIndex(r => r.TenantId);
        });

        builder.Entity<CommissionEntry>(e =>
        {
            e.HasOne(ce => ce.Tenant).WithMany(t => t.CommissionEntries)
             .HasForeignKey(ce => ce.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ce => ce.Agent).WithMany()
             .HasForeignKey(ce => ce.AgentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(ce => ce.Payment).WithMany()
             .HasForeignKey(ce => ce.PaymentId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(ce => ce.Lead).WithMany()
             .HasForeignKey(ce => ce.LeadId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(ce => ce.Rule).WithMany(r => r.Entries)
             .HasForeignKey(ce => ce.RuleId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.Property(ce => ce.Amount).HasColumnType("decimal(18,2)");
            e.HasIndex(ce => new { ce.TenantId, ce.AgentId });
        });

        // ?? Post-Call Disposition Forms ???????????????????????????????????????
        builder.Entity<DispositionForm>(e =>
        {
            e.HasOne(f => f.Tenant).WithMany(t => t.DispositionForms)
             .HasForeignKey(f => f.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(f => f.Campaign).WithMany()
             .HasForeignKey(f => f.CampaignId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(f => f.TenantId);
        });

        builder.Entity<DispositionField>(e =>
        {
            e.HasOne(f => f.Form).WithMany(fm => fm.Fields)
             .HasForeignKey(f => f.FormId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(f => f.FormId);
        });

        builder.Entity<DispositionResponse>(e =>
        {
            e.HasOne(r => r.Tenant).WithMany()
             .HasForeignKey(r => r.TenantId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(r => r.Form).WithMany(f => f.Responses)
             .HasForeignKey(r => r.FormId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Call).WithMany()
             .HasForeignKey(r => r.CallId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Agent).WithMany()
             .HasForeignKey(r => r.AgentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Lead).WithMany()
             .HasForeignKey(r => r.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.Property(r => r.AnswersJson).HasColumnType("LONGTEXT");
            e.HasIndex(r => r.CallId);
        });

        // ?? NPS Surveys ????????????????????????????????????????????????????????
        builder.Entity<NpsSurvey>(e =>
        {
            e.HasOne(s => s.Tenant).WithMany(t => t.NpsSurveys)
             .HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Campaign).WithMany()
             .HasForeignKey(s => s.CampaignId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(s => s.TenantId);
        });

        builder.Entity<NpsSurveyResponse>(e =>
        {
            e.HasOne(r => r.Survey).WithMany(s => s.Responses)
             .HasForeignKey(r => r.SurveyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Lead).WithMany()
             .HasForeignKey(r => r.LeadId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Agent).WithMany()
             .HasForeignKey(r => r.AgentId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(r => r.Call).WithMany()
             .HasForeignKey(r => r.CallId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.Ignore(r => r.TenantId);
            e.HasIndex(r => r.SurveyId);
        });

        // ?? Calendar Sync ?????????????????????????????????????????????????????
        builder.Entity<CalendarSyncConfig>(e =>
        {
            e.HasOne(c => c.User).WithMany()
             .HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => c.UserId).IsUnique();
        });
    }
}
