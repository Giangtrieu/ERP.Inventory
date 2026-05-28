using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;

namespace ERP.Inventory.Application.Interfaces;

public interface ITrackingService
{
    Task<ServiceResult<PagedResult<TrackingSearchResultDto>>> SearchAsync(string keyword, int page, int pageSize, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<ServiceResult<PagedResult<MovementHistoryDto>>> GetHistoryAsync(int itemInstanceId, int page, int pageSize, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<ServiceResult<PagedResult<InventoryListRowDto>>> GetInventoryListAsync(string? keyword, int? warehouseId, int? categoryId, string? status, int page, int pageSize, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<ServiceResult<PagedResult<InventoryListRowDto>>> GetListInventoryAsync(string? keyword, int? warehouseId, int?itemId, string? status, int page, int pageSize, CurrentUserContext user, CancellationToken cancellationToken = default);
}
