using ERP.Inventory.Application.DTOs;
using ERP.Inventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Inventory.Web.Controllers;

[Route("[controller]")]
[Authorize(Roles = "Admin,Warehouse Manager,Warehouse Staff")]
public sealed class BorrowController : Controller
{
    private readonly IBorrowService _borrowService;
    private readonly ICurrentUserService _currentUserService;

    public BorrowController(IBorrowService borrowService, ICurrentUserService currentUserService)
    {
        _borrowService = borrowService;
        _currentUserService = currentUserService;
    }

    [HttpPost("Lend")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lend([FromBody] BorrowLendRequest request, CancellationToken cancellationToken)
    {
        var result = await _borrowService.LendAsync(request, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }

    [HttpPost("Return")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnBorrowed([FromBody] BorrowReturnRequest request, CancellationToken cancellationToken)
    {
        var result = await _borrowService.ReturnAsync(request, _currentUserService.GetCurrentUser(), cancellationToken);
        return Json(result);
    }
}
