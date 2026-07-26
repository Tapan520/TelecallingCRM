using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;
namespace TelecallingCRM.Pages.Admin;
[Authorize(Roles = "admin,superadmin")] [RequireModule(CrmModule.ApiKeys)] public class ApiKeysModel : PageModel { public void OnGet() { } }
