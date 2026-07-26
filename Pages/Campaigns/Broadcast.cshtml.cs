using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Pages.Campaigns;

[Authorize]
[RequireModule(CrmModule.Broadcast)]
public class BroadcastModel : PageModel { public void OnGet() { } }
