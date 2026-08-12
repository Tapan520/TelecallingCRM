using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;
namespace TelecallingCRM.Pages.Leads;
[Authorize(Roles = "admin")] [RequireModule(CrmModule.LeadImport)]
public class ImportModel : PageModel { public void OnGet() { } }
