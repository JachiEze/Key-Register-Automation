using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using KEYREGISTERAUTOMATION.Data;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KEYREGISTERAUTOMATION.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string? GetCurrentIGG()
        {
            var windowsName = User.Identity?.Name;

            if (string.IsNullOrEmpty(windowsName))
                return null;

            return windowsName.Split('\\').Last().ToUpper();
        }
    }
}
