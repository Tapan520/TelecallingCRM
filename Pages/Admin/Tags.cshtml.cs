using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;
namespace TelecallingCRM.Pages.Admin;
[Authorize] [RequireModule(CrmModule.Tags)] public class TagsModel : PageModel { public void OnGet() { } }
