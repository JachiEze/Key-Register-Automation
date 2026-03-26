using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using KEYREGISTERAUTOMATION.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Linq;
using KEYREGISTERAUTOMATION.Models;

[Authorize]
public class BaseController : Controller
{
    protected readonly ApplicationDbContext _context;

    public BaseController(ApplicationDbContext context)
    {
        _context = context;
    }

    protected async Task<VwStaff?> GetCurrentUserAsync()
    {
        var windowsName = User.Identity?.Name;

        if (string.IsNullOrEmpty(windowsName))
            return null;

        var igg = windowsName.Split('\\').Last().ToUpper();

        return await _context.vwstaff
            .FirstOrDefaultAsync(s => s.IGG.ToUpper() == igg);
    }

    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var user = await GetCurrentUserAsync();

        if (user != null)
        {
            ViewBag.CurrentUserFullName = $"{user.Name}";
        }

        await next();
    }
}