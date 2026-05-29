using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Inventory.Infrastructure.Data.Migrations
{
    public partial class AddInboundQuantityLifecycleBatch : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LifecycleBatchId",
                table: "QuantityInventoryTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LifecycleBatchId",
                table: "QuantityInventoryDocumentLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "InboundDocumentLogs",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "LifecycleBatchId",
                table: "InboundDocumentLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE InboundDocumentLogs
SET LifecycleBatchId = CONVERT(uniqueidentifier, CONVERT(binary(16), HASHBYTES('MD5', CONCAT('Inbound|', InboundDocumentId, '|', Action, '|', CONVERT(varchar(33), [Timestamp], 126), '|', PerformedBy))))
WHERE LifecycleBatchId IS NULL;

UPDATE QuantityInventoryTransactions
SET LifecycleBatchId = CONVERT(uniqueidentifier, CONVERT(binary(16), HASHBYTES('MD5', CONCAT('Quantity|', DocumentId, '|', TransactionType, '|', CONVERT(varchar(33), PostedAt, 126), '|', PostedBy))))
WHERE LifecycleBatchId IS NULL;

UPDATE h
SET LifecycleBatchId = l.LifecycleBatchId
FROM ItemMovementHistories h
JOIN InboundDocumentLogs l
  ON l.InboundDocumentId = h.DocumentId
 AND l.ItemInstanceId = h.ItemInstanceId
WHERE h.DocumentType = 'InboundDocument'
  AND h.ActionType = 1
  AND h.LifecycleBatchId IS NULL
  AND l.LifecycleBatchId IS NOT NULL;

UPDATE t
SET LifecycleBatchId = l.LifecycleBatchId
FROM InventoryTransactions t
JOIN InboundDocumentLogs l
  ON l.InboundDocumentId = t.DocumentId
 AND l.ItemInstanceId = t.ItemInstanceId
WHERE t.DocumentType = 'InboundDocument'
  AND t.TransactionType = 1
  AND t.LifecycleBatchId IS NULL
  AND l.LifecycleBatchId IS NOT NULL;

UPDATE ql
SET LifecycleBatchId = tx.LifecycleBatchId
FROM QuantityInventoryDocumentLines ql
OUTER APPLY (
    SELECT TOP (1) qt.LifecycleBatchId
    FROM QuantityInventoryTransactions qt
    WHERE qt.DocumentId = ql.QuantityInventoryDocumentId
      AND qt.ItemId = ql.ItemId
      AND qt.SnCode = ql.SnCode
      AND qt.LifecycleBatchId IS NOT NULL
    ORDER BY qt.PostedAt DESC, qt.Id DESC
) tx
WHERE ql.LifecycleBatchId IS NULL
  AND tx.LifecycleBatchId IS NOT NULL;
");

            migrationBuilder.CreateIndex(
                name: "IX_QuantityInventoryTransactions_DocumentId_TransactionType_LifecycleBatchId",
                table: "QuantityInventoryTransactions",
                columns: new[] { "DocumentId", "TransactionType", "LifecycleBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuantityInventoryDocumentLines_QuantityInventoryDocumentId_LifecycleBatchId",
                table: "QuantityInventoryDocumentLines",
                columns: new[] { "QuantityInventoryDocumentId", "LifecycleBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_InboundDocumentLogs_InboundDocumentId_Action_LifecycleBatchId",
                table: "InboundDocumentLogs",
                columns: new[] { "InboundDocumentId", "Action", "LifecycleBatchId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuantityInventoryTransactions_DocumentId_TransactionType_LifecycleBatchId",
                table: "QuantityInventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_QuantityInventoryDocumentLines_QuantityInventoryDocumentId_LifecycleBatchId",
                table: "QuantityInventoryDocumentLines");

            migrationBuilder.DropIndex(
                name: "IX_InboundDocumentLogs_InboundDocumentId_Action_LifecycleBatchId",
                table: "InboundDocumentLogs");

            migrationBuilder.DropColumn(
                name: "LifecycleBatchId",
                table: "QuantityInventoryTransactions");

            migrationBuilder.DropColumn(
                name: "LifecycleBatchId",
                table: "QuantityInventoryDocumentLines");

            migrationBuilder.DropColumn(
                name: "LifecycleBatchId",
                table: "InboundDocumentLogs");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "InboundDocumentLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
