using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Pages.Messaging;

[Authorize]
[RequireModule(CrmModule.Email)]
public class EmailPageModel : PageModel
{
    public void OnGet() { }
}
