using Microsoft.AspNetCore.Mvc;
using KEYREGISTERAUTOMATION.Data;
using Microsoft.AspNetCore.Authorization;

namespace KEYREGISTERAUTOMATION.Controllers
{
    [Authorize(Roles = "Requester")]
    public class RequesterController : BaseController
    {
        public RequesterController(ApplicationDbContext context) : base(context) { }

        public IActionResult Index()
        {
            return View();
        }
    }
}