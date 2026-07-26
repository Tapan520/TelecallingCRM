using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;
namespace TelecallingCRM.Pages.Messaging;
[Authorize] [RequireModule(CrmModule.Inbox)]
public class InboxModel : PageModel { public void OnGet() { } }
