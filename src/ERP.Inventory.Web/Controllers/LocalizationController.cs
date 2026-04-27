using ERP.Inventory.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Inventory.Web.Controllers;

[Authorize]
[Route("[controller]")]
public sealed class LocalizationController : Controller
{
    [HttpGet("Resources")]
    public IActionResult Resources([FromQuery] string lang = "vi")
    {
        return Json(LocalizationCatalog.Resources(lang));
    }

    [HttpGet("Enums")]
    public IActionResult Enums([FromQuery] string lang = "vi")
    {
        return Json(LocalizationCatalog.AllEnumOptions(lang));
    }
}
