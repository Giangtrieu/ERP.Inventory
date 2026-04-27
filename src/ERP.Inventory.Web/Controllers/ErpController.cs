using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ERP.Inventory.Web.Controllers;

[Authorize]
public sealed class ErpController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
