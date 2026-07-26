namespace TelecallingCRM.Data.Models;

/// <summary>
/// Catalogue of every product module that can be toggled per tenant by the SuperAdmin.
/// </summary>
public enum CrmModule
{
    // Sales
    Leads          = 1,
    LeadImport     = 2,
    Pipeline       = 3,
    Campaigns      = 4,
    Broadcast      = 5,
    Dialer         = 6,
    CallLog        = 7,
    LiveMonitor    = 8,
    Payments       = 9,

    // Messaging
    WhatsApp       = 10,
    Sms            = 11,
    Email          = 12,
    Templates      = 13,
    Inbox          = 14,

    // Sales Intelligence
    Deals          = 20,
    Quotations     = 21,
    Invoices       = 22,
    Commissions    = 23,

    // Automation
    DripAutomation = 30,
    Disposition    = 31,
    NpsSurveys     = 32,

    // Productivity
    Meetings       = 40,
    FollowUps      = 41,
    Tasks          = 42,
    Escalations    = 43,
    Documents      = 44,
    Attendance     = 45,
    Leaves         = 46,
    HolidayCalendar= 47,
    Announcements  = 48,
    Expenses       = 49,
    Gamification   = 50,
    Onboarding     = 51,
    ShiftSwap      = 52,
    CalendarSync   = 53,

    // Analytics
    Reports        = 60,
    Leaderboard    = 61,
    Revenue        = 62,
    ActivityFeed   = 63,

    // AI Tools
    AiInsights     = 70,
    AiAssistant    = 71,
    KnowledgeBase  = 72,

    // Admin tools (always visible to admin/manager, toggled separately)
    AgentGoals     = 80,
    AgentShifts    = 81,
    CallQuality    = 82,
    DncList        = 83,
    CustomFields   = 84,
    Tags           = 85,
    ApiKeys        = 86,
    ExportCenter   = 87,
    Integrations   = 88,
    Webhooks       = 89,
    CrmSync        = 90,
    GlobalSearch   = 91,
    CallScripts    = 92,
}

/// <summary>
/// Stores the enabled/disabled state of a module for a specific tenant.
/// Missing rows are treated as ENABLED (opt-out model — all modules on by default).
/// </summary>
public class TenantModuleAccess
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public CrmModule Module { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
}
