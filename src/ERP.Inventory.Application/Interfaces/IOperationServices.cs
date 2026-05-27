using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;
using System.Text.Json;

namespace ERP.Inventory.Application.Interfaces;

public interface IInboundService
{
    Task<ServiceResult<PostedDocumentDto>> CreateInboundAsync(InboundRequest request, CurrentUserContext user, CancellationToken cancellationToken = default);
}

public interface IInventoryOperationService
{
    Task<ServiceResult<PostedDocumentDto>> MoveLocationAsync(MoveLocationRequest request, CurrentUserContext user, CancellationToken cancellationToken = default);
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

public interface IQuantityInventoryService
{
    Task<ServiceResult<PostedDocumentDto>> ReceiveAsync(QuantityInventoryRequest request, CurrentUserContext user, CancellationToken cancellationToken = default, bool isEdit = false);
    Task<ServiceResult<PostedDocumentDto>> IssueAsync(QuantityInventoryRequest request, CurrentUserContext user, CancellationToken cancellationToken = default, bool isEdit = false);
    Task<ServiceResult<PostedDocumentDto>> AdjustAsync(QuantityInventoryRequest request, CurrentUserContext user, CancellationToken cancellationToken = default, bool isEdit = false);
    Task<PagedResult<QuantityStockBalanceDto>> GetBalancesAsync(string? keyword, int? warehouseId, int? itemId, string? status, string? ownerName, int page, int pageSize, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<QuantityInventoryTransactionDto>> GetTransactionsAsync(string? keyword, int? warehouseId, int? itemId, int take, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<QuantityInstanceDto>> GetInstancesAsync(string? itemCode, int? warehouseId, string? ownerName, CurrentUserContext user, CancellationToken cancellationToken = default);
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
    /// <summary>Tóm tắt tồn kho QuantityOnly — card + charts cho dashboard.</summary>
    Task<QuantitySummaryDto> GetQuantitySummaryAsync(int? warehouseId, CurrentUserContext user, CancellationToken cancellationToken = default);
}

public interface IDocumentLifecycleService
{
    Task<ServiceResult<DocumentMutationResultDto>> DeleteAsync(string type, int id, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<ServiceResult<DocumentMutationResultDto>> EditAsync(string type, int id, JsonElement payload, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<ServiceResult<DocumentMutationResultDto>> RebuildAsync(string type, int id, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<ServiceResult<DocumentDependencyDto>> PreviewDependenciesAsync(string type, int id, string action, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<ServiceResult<DocumentEditModelDto>> GetEditModelAsync(string type, int id, CurrentUserContext user, CancellationToken cancellationToken = default);
}
