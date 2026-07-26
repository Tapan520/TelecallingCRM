using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Pages.SuperAdmin;

[Authorize(Roles = "superadmin")]
public class TenantModulesModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantModuleService _moduleSvc;

    public TenantModulesModel(AppDbContext db, ITenantModuleService moduleSvc)
    {
        _db = db;
        _moduleSvc = moduleSvc;
    }

    [BindProperty(SupportsGet = true)]
    public Guid TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;
    public string TenantSlug { get; set; } = string.Empty;
    public string TenantPlan { get; set; } = string.Empty;

    // Grouped module display model
    public Dictionary<string, List<ModuleItem>> ModuleGroups { get; set; } = new();

    [BindProperty]
    public List<string> EnabledModules { get; set; } = new();

    public record ModuleItem(string Name, string Label, int Value, bool IsEnabled);

    // Friendly display names + section grouping for every CrmModule value
    private static readonly Dictionary<CrmModule, (string Label, string Group)> ModuleMeta = new()
    {
        { CrmModule.Leads,           ("Leads",            "Sales") },
        { CrmModule.LeadImport,      ("Lead Import",      "Sales") },
        { CrmModule.Pipeline,        ("Pipeline",         "Sales") },
        { CrmModule.Campaigns,       ("Campaigns",        "Sales") },
        { CrmModule.Broadcast,       ("Broadcast",        "Sales") },
        { CrmModule.Dialer,          ("Dialer",           "Sales") },
        { CrmModule.CallLog,         ("Call Log",         "Sales") },
        { CrmModule.LiveMonitor,     ("Live Monitor",     "Sales") },
        { CrmModule.Payments,        ("Payments",         "Sales") },
        { CrmModule.WhatsApp,        ("WhatsApp",         "Messaging") },
        { CrmModule.Sms,             ("SMS",              "Messaging") },
        { CrmModule.Email,           ("Email",            "Messaging") },
        { CrmModule.Templates,       ("Templates",        "Messaging") },
        { CrmModule.Inbox,           ("Inbox",            "Messaging") },
        { CrmModule.Deals,           ("Deal Pipeline",    "Sales Intelligence") },
        { CrmModule.Quotations,      ("Quotations",       "Sales Intelligence") },
        { CrmModule.Invoices,        ("Invoices",         "Sales Intelligence") },
        { CrmModule.Commissions,     ("Commissions",      "Sales Intelligence") },
        { CrmModule.DripAutomation,  ("Drip Automation",  "Automation") },
        { CrmModule.Disposition,     ("Disposition Forms","Automation") },
        { CrmModule.NpsSurveys,      ("NPS Surveys",      "Automation") },
        { CrmModule.Meetings,        ("Meetings",         "Productivity") },
        { CrmModule.FollowUps,       ("Follow-ups",       "Productivity") },
        { CrmModule.Tasks,           ("Tasks",            "Productivity") },
        { CrmModule.Escalations,     ("Escalations",      "Productivity") },
        { CrmModule.Documents,       ("Documents",        "Productivity") },
        { CrmModule.Attendance,      ("Attendance",       "Productivity") },
        { CrmModule.Leaves,          ("Leaves",           "Productivity") },
        { CrmModule.HolidayCalendar, ("Holiday Calendar", "Productivity") },
        { CrmModule.Announcements,   ("Announcements",    "Productivity") },
        { CrmModule.Expenses,        ("Expenses",         "Productivity") },
        { CrmModule.Gamification,    ("Badges & Points",  "Productivity") },
        { CrmModule.Onboarding,      ("Onboarding",       "Productivity") },
        { CrmModule.ShiftSwap,       ("Shift Swap",       "Productivity") },
        { CrmModule.CalendarSync,    ("Calendar Sync",    "Productivity") },
        { CrmModule.Reports,         ("Reports",          "Analytics") },
        { CrmModule.Leaderboard,     ("Leaderboard",      "Analytics") },
        { CrmModule.Revenue,         ("Revenue",          "Analytics") },
        { CrmModule.ActivityFeed,    ("Activity Feed",    "Analytics") },
        { CrmModule.AiInsights,      ("AI Insights",      "AI Tools") },
        { CrmModule.AiAssistant,     ("AI Assistant",     "AI Tools") },
        { CrmModule.KnowledgeBase,   ("Knowledge Base",   "AI Tools") },
        { CrmModule.AgentGoals,      ("Agent Goals",      "Admin Tools") },
        { CrmModule.AgentShifts,     ("Agent Shifts",     "Admin Tools") },
        { CrmModule.CallQuality,     ("Call Quality",     "Admin Tools") },
        { CrmModule.DncList,         ("DNC List",         "Admin Tools") },
        { CrmModule.CustomFields,    ("Custom Fields",    "Admin Tools") },
        { CrmModule.Tags,            ("Tags",             "Admin Tools") },
        { CrmModule.ApiKeys,         ("API Keys",         "Admin Tools") },
        { CrmModule.ExportCenter,    ("Export Center",    "Admin Tools") },
        { CrmModule.Integrations,    ("Integrations",     "Admin Tools") },
        { CrmModule.Webhooks,        ("Webhooks",         "Admin Tools") },
        { CrmModule.CrmSync,         ("CRM Sync",         "Admin Tools") },
        { CrmModule.GlobalSearch,    ("Global Search",    "Admin Tools") },
        { CrmModule.CallScripts,     ("Call Scripts",     "Admin Tools") },
    };

    public async Task<IActionResult> OnGetAsync()
    {
        var tenant = await _db.Tenants.FindAsync(TenantId);
        if (tenant == null) return NotFound();

        TenantName = tenant.Name;
        TenantSlug = tenant.Slug;
        TenantPlan = tenant.Plan;

        var enabledSet = await _moduleSvc.GetEnabledModulesAsync(TenantId);
        BuildGroups(enabledSet);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var tenant = await _db.Tenants.FindAsync(TenantId);
        if (tenant == null) return NotFound();

        var parsed = EnabledModules
            .Select(n => Enum.TryParse<CrmModule>(n, true, out var m) ? (CrmModule?)m : null)
            .Where(m => m.HasValue)
            .Select(m => m!.Value);

        await _moduleSvc.SetModulesAsync(TenantId, parsed);

        TempData["Success"] = "Module access updated successfully.";
        return RedirectToPage(new { TenantId });
    }

    private void BuildGroups(HashSet<CrmModule> enabledSet)
    {
        ModuleGroups = new Dictionary<string, List<ModuleItem>>();
        foreach (var kvp in ModuleMeta.OrderBy(x => (int)x.Key))
        {
            var (label, group) = kvp.Value;
            if (!ModuleGroups.ContainsKey(group))
                ModuleGroups[group] = new();

            ModuleGroups[group].Add(new ModuleItem(
                kvp.Key.ToString(), label, (int)kvp.Key, enabledSet.Contains(kvp.Key)));
        }
    }
}
