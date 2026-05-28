using ERP.Inventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Inventory.Web.Controllers;

[Route("[controller]")]
[Authorize]
public sealed class TrackingController : Controller
{
    private readonly ITrackingService _trackingService;
    private readonly ICurrentUserService _currentUserService;

    public TrackingController(ITrackingService trackingService, ICurrentUserService currentUserService)
    {
        _trackingService = trackingService;
        _currentUserService = currentUserService;
    }

    [HttpGet("Search")]
    public async Task<IActionResult> Search([FromQuery] string keyword, [FromQuery] int page = 1,[FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await _trackingService.SearchAsync(keyword, page, pageSize, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpGet("History/{itemInstanceId:int}")]
    public async Task<IActionResult> History(int itemInstanceId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _trackingService.GetHistoryAsync(itemInstanceId, page, pageSize, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }
}
