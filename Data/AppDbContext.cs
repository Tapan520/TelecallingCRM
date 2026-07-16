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
    }
}
