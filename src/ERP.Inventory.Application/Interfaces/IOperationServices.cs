using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;

namespace ERP.Inventory.Application.Interfaces;

public interface IInboundService
{
    Task<ServiceResult<PostedDocumentDto>> CreateInboundAsync(InboundRequest request, CurrentUserContext user, CancellationToken cancellationToken = default);
}

public interface IInventoryOperationService
{
    Task<ServiceResult<PostedDocumentDto>> MoveLocationAsync(MoveLocationRequest request, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<ServiceResult<PostedDocumentDto>> AdjustAsync(AdjustmentRequest request, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<ServiceResult<PostedDocumentDto>> CreateInventoryCheckAsync(InventoryCheckRequest request, CurrentUserContext user, CancellationToken cancellationToken = default);
}

public interface IRepairService
{
    Task<ServiceResult<PostedDocumentDto>> SendToRepairAsync(RepairSendRequest request, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<ServiceResult<PostedDocumentDto>> ReceiveFromRepairAsync(RepairReceiveRequest request, CurrentUserContext user, CancellationToken cancellationToken = default);
}

public interface IBorrowService
{
    Task<ServiceResult<PostedDocumentDto>> LendAsync(BorrowLendRequest request, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<ServiceResult<PostedDocumentDto>> ReturnAsync(BorrowReturnRequest request, CurrentUserContext user, CancellationToken cancellationToken = default);
}

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(int? warehouseId, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ChartPointDto>> GetStockByWarehouseAsync(string? status, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ChartPointDto>> GetStockByStatusAsync(int? warehouseId, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ChartPointDto>> GetMovementTrendAsync(int? warehouseId, int days, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ChartPointDto>> GetMovementByActionAsync(int? warehouseId, int days, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ChartPointDto>> GetStockByCategoryAsync(int? warehouseId, string? status, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ChartPointDto>> GetLocationUtilizationAsync(int? warehouseId, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ChartPointDto>> GetOverdueBorrowAgingAsync(int? warehouseId, CurrentUserContext user, CancellationToken cancellationToken = default);
}
