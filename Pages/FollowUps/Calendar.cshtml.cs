using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;
namespace TelecallingCRM.Pages.FollowUps;
[Authorize] [RequireModule(CrmModule.FollowUps)]
public class CalendarModel : PageModel { public void OnGet() { } }
