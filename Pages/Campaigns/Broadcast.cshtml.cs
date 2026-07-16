using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Campaigns;

[Authorize]
public class BroadcastModel : PageModel { public void OnGet() { } }
