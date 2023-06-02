using Juice.Tenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Juice.MultiTenant.SelfHost.Controllers
{
    [Authorize("home")]
    public class HomeController : Controller
    {
        public IActionResult Index([FromServices] ITenant? tenant = null)
        {
            return Json(new { Message = $"Hello World! {tenant?.Identifier ?? "root"}" });
        }
    }
}
