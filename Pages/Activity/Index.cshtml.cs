using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;
namespace TelecallingCRM.Pages.ActivityFeed;
[Authorize] [RequireModule(CrmModule.ActivityFeed)]
public class IndexModel : PageModel { public void OnGet() { } }
