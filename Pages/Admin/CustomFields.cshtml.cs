using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Pages.Admin;

[Authorize(Roles = "admin,superadmin")]
[RequireModule(CrmModule.CustomFields)]
public class CustomFieldsModel : PageModel
{
    public void OnGet() { }
}