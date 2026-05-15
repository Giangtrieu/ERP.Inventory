using ERP.Inventory.Application.Common;
using ERP.Inventory.Application.DTOs;

namespace ERP.Inventory.Application.Interfaces;

public interface IImportService
{
    Task<ServiceResult<int>> UploadAsync(string importType, string fileName, Stream fileStream, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<ServiceResult<int>> ValidateAsync(int importBatchId, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<ServiceResult<int>> ConfirmAsync(int importBatchId, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyCollection<ImportBatchDto>>> ListAsync(CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyCollection<ImportValidationRowDto>>> RowsAsync(int importBatchId, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<byte[]> TemplateAsync(string importType, CurrentUserContext user, CancellationToken cancellationToken = default);
}

public interface IExportService
{
    Task<byte[]> ExportInventoryAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<byte[]> ExportHistoryAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<byte[]> ExportAuditAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default);
    // Phase 6 — new exports
    Task<byte[]> ExportQuantityBalanceAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<byte[]> ExportInboundDocumentsAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<byte[]> ExportBorrowDocumentsAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<byte[]> ExportRepairDocumentsAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<byte[]> ExportItemMasterAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<byte[]> ExportWarehouseStructureAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<byte[]> ExportQuantityTransactionsAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<byte[]> ExportMoveDocumentsAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<byte[]> ExportAdjustmentDocumentsAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default);
    Task<byte[]> ExportInventoryCheckDocumentsAsync(ExportFilterDto filter, CurrentUserContext user, CancellationToken cancellationToken = default);
}

public interface ICurrentUserService
{
    CurrentUserContext GetCurrentUser();
}
