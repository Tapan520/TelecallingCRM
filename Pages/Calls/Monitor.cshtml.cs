using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Calls;

[Authorize(Roles = "admin,manager,superadmin")]
public class MonitorModel : PageModel { public void OnGet() { } }
