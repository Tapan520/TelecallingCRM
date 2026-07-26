using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Pages.Reports;

[Authorize]
[RequireModule(CrmModule.Revenue)]
public class RevenueModel : PageModel
{
    public void OnGet() { }
}
