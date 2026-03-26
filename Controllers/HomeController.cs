using KEYREGISTERAUTOMATION.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

[Authorize]
public class HomeController : BaseController
{
    public HomeController(ApplicationDbContext context)
        : base(context)
    {
    }

    public async Task<IActionResult> Index()
    {
        var windowsName = User.Identity?.Name;

        if (string.IsNullOrEmpty(windowsName))
            return Unauthorized();

        var igg = windowsName.Split('\\').Last().ToUpper();

        var user = await _context.UserAccounts
            .FirstOrDefaultAsync(u => u.IGG.ToUpper() == igg);

        if (user == null)
            return View("AccessDenied");

        switch (user.Role)
        {
            case "Administrator":
                return RedirectToAction("Index", "Administrator");

            case "Facility Manager":
                return RedirectToAction("Index", "FacilityManager");

            case "Requester":
                return RedirectToAction("Index", "Requester");

            default:
                return View("AccessDenied");
        }
    }
}