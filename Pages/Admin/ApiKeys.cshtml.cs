using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace TelecallingCRM.Pages.Admin;
[Authorize(Roles = "admin,superadmin")] public class ApiKeysModel : PageModel { public void OnGet() { } }
