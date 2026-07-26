using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Pages.Calls;

[Authorize(Roles = "admin,manager,superadmin")]
[RequireModule(CrmModule.LiveMonitor)]
public class MonitorModel : PageModel { public void OnGet() { } }
