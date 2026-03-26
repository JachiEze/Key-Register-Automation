using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KEYREGISTERAUTOMATION.Data;
using Microsoft.AspNetCore.Authorization;

namespace KEYREGISTERAUTOMATION.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AdministratorController : BaseController
    {
        public AdministratorController(ApplicationDbContext context) : base(context) { }

        public IActionResult Index()
        {
            return View();
        }
    }
}