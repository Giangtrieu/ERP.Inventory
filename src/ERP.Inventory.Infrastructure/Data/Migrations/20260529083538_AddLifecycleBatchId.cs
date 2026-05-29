using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Inventory.Infrastructure.Data.Migrations
{
    public partial class AddLifecycleBatchId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "RepairDocumentLogs",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "LifecycleBatchId",
                table: "RepairDocumentLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LifecycleBatchId",
                table: "ItemMovementHistories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LifecycleBatchId",
                table: "InventoryTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "BorrowDocumentLogs",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "LifecycleBatchId",
                table: "BorrowDocumentLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE BorrowDocumentLogs
SET LifecycleBatchId = CONVERT(uniqueidentifier, CONVERT(binary(16), HASHBYTES('MD5', CONCAT('Borrow|', BorrowDocumentId, '|', Action, '|', CONVERT(varchar(33), [Timestamp], 126), '|', PerformedBy))))
WHERE LifecycleBatchId IS NULL;

UPDATE RepairDocumentLogs
SET LifecycleBatchId = CONVERT(uniqueidentifier, CONVERT(binary(16), HASHBYTES('MD5', CONCAT('Repair|', RepairDocumentId, '|', Action, '|', CONVERT(varchar(33), [Timestamp], 126), '|', PerformedBy))))
WHERE LifecycleBatchId IS NULL;
");

            migrationBuilder.Sql(@"
WITH BorrowLogs AS
(
    SELECT
        BorrowDocumentId AS DocumentId,
        ItemInstanceId,
        Action,
        LifecycleBatchId,
        ROW_NUMBER() OVER (PARTITION BY BorrowDocumentId, ItemInstanceId, Action ORDER BY [Timestamp], Id) AS RowNo
    FROM BorrowDocumentLogs
    WHERE LifecycleBatchId IS NOT NULL
),
BorrowHistories AS
(
    SELECT
        Id,
        DocumentId,
        ItemInstanceId,
        CASE ActionType
            WHEN 5 THEN 'BorrowIssue'
            WHEN 6 THEN 'BorrowReturn'
        END AS Action,
        ROW_NUMBER() OVER (PARTITION BY DocumentId, ItemInstanceId, ActionType ORDER BY PerformedAt, Id) AS RowNo
    FROM ItemMovementHistories
    WHERE LifecycleBatchId IS NULL
      AND DocumentType = 'BorrowDocument'
      AND ActionType IN (5, 6)
)
UPDATE h
SET LifecycleBatchId = l.LifecycleBatchId
FROM ItemMovementHistories h
JOIN BorrowHistories bh ON bh.Id = h.Id
JOIN BorrowLogs l
  ON l.DocumentId = bh.DocumentId
 AND l.ItemInstanceId = bh.ItemInstanceId
 AND l.Action = bh.Action
 AND l.RowNo = bh.RowNo;

WITH RepairLogs AS
(
    SELECT
        RepairDocumentId AS DocumentId,
        ItemInstanceId,
        Action,
        LifecycleBatchId,
        ROW_NUMBER() OVER (PARTITION BY RepairDocumentId, ItemInstanceId, Action ORDER BY [Timestamp], Id) AS RowNo
    FROM RepairDocumentLogs
    WHERE LifecycleBatchId IS NOT NULL
),
RepairHistories AS
(
    SELECT
        Id,
        DocumentId,
        ItemInstanceId,
        CASE ActionType
            WHEN 3 THEN 'RepairSend'
            WHEN 4 THEN 'RepairReceive'
        END AS Action,
        ROW_NUMBER() OVER (PARTITION BY DocumentId, ItemInstanceId, ActionType ORDER BY PerformedAt, Id) AS RowNo
    FROM ItemMovementHistories
    WHERE LifecycleBatchId IS NULL
      AND DocumentType = 'RepairDocument'
      AND ActionType IN (3, 4)
)
UPDATE h
SET LifecycleBatchId = l.LifecycleBatchId
FROM ItemMovementHistories h
JOIN RepairHistories rh ON rh.Id = h.Id
JOIN RepairLogs l
  ON l.DocumentId = rh.DocumentId
 AND l.ItemInstanceId = rh.ItemInstanceId
 AND l.Action = rh.Action
 AND l.RowNo = rh.RowNo;
");

            migrationBuilder.Sql(@"
WITH BorrowLogs AS
(
    SELECT
        BorrowDocumentId AS DocumentId,
        ItemInstanceId,
        Action,
        LifecycleBatchId,
        ROW_NUMBER() OVER (PARTITION BY BorrowDocumentId, ItemInstanceId, Action ORDER BY [Timestamp], Id) AS RowNo
    FROM BorrowDocumentLogs
    WHERE LifecycleBatchId IS NOT NULL
),
BorrowTransactions AS
(
    SELECT
        Id,
        DocumentId,
        ItemInstanceId,
        CASE TransactionType
            WHEN 5 THEN 'BorrowIssue'
            WHEN 6 THEN 'BorrowReturn'
        END AS Action,
        ROW_NUMBER() OVER (PARTITION BY DocumentId, ItemInstanceId, TransactionType ORDER BY PostedAt, Id) AS RowNo
    FROM InventoryTransactions
    WHERE LifecycleBatchId IS NULL
      AND DocumentType = 'BorrowDocument'
      AND TransactionType IN (5, 6)
      AND ItemInstanceId IS NOT NULL
)
UPDATE t
SET LifecycleBatchId = l.LifecycleBatchId
FROM InventoryTransactions t
JOIN BorrowTransactions bt ON bt.Id = t.Id
JOIN BorrowLogs l
  ON l.DocumentId = bt.DocumentId
 AND l.ItemInstanceId = bt.ItemInstanceId
 AND l.Action = bt.Action
 AND l.RowNo = bt.RowNo;

WITH RepairLogs AS
(
    SELECT
        RepairDocumentId AS DocumentId,
        ItemInstanceId,
        Action,
        LifecycleBatchId,
        ROW_NUMBER() OVER (PARTITION BY RepairDocumentId, ItemInstanceId, Action ORDER BY [Timestamp], Id) AS RowNo
    FROM RepairDocumentLogs
    WHERE LifecycleBatchId IS NOT NULL
),
RepairTransactions AS
(
    SELECT
        Id,
        DocumentId,
        ItemInstanceId,
        CASE TransactionType
            WHEN 3 THEN 'RepairSend'
            WHEN 4 THEN 'RepairReceive'
        END AS Action,
        ROW_NUMBER() OVER (PARTITION BY DocumentId, ItemInstanceId, TransactionType ORDER BY PostedAt, Id) AS RowNo
    FROM InventoryTransactions
    WHERE LifecycleBatchId IS NULL
      AND DocumentType = 'RepairDocument'
      AND TransactionType IN (3, 4)
      AND ItemInstanceId IS NOT NULL
)
UPDATE t
SET LifecycleBatchId = l.LifecycleBatchId
FROM InventoryTransactions t
JOIN RepairTransactions rt ON rt.Id = t.Id
JOIN RepairLogs l
  ON l.DocumentId = rt.DocumentId
 AND l.ItemInstanceId = rt.ItemInstanceId
 AND l.Action = rt.Action
 AND l.RowNo = rt.RowNo;
");

            migrationBuilder.CreateIndex(
                name: "IX_RepairDocumentLogs_RepairDocumentId_Action_LifecycleBatchId",
                table: "RepairDocumentLogs",
                columns: new[] { "RepairDocumentId", "Action", "LifecycleBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemMovementHistories_DocumentType_DocumentId_ActionType_LifecycleBatchId",
                table: "ItemMovementHistories",
                columns: new[] { "DocumentType", "DocumentId", "ActionType", "LifecycleBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_DocumentType_DocumentId_TransactionType_LifecycleBatchId",
                table: "InventoryTransactions",
                columns: new[] { "DocumentType", "DocumentId", "TransactionType", "LifecycleBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_BorrowDocumentLogs_BorrowDocumentId_Action_LifecycleBatchId",
                table: "BorrowDocumentLogs",
                columns: new[] { "BorrowDocumentId", "Action", "LifecycleBatchId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RepairDocumentLogs_RepairDocumentId_Action_LifecycleBatchId",
                table: "RepairDocumentLogs");

            migrationBuilder.DropIndex(
                name: "IX_ItemMovementHistories_DocumentType_DocumentId_ActionType_LifecycleBatchId",
                table: "ItemMovementHistories");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_DocumentType_DocumentId_TransactionType_LifecycleBatchId",
                table: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BorrowDocumentLogs_BorrowDocumentId_Action_LifecycleBatchId",
                table: "BorrowDocumentLogs");

            migrationBuilder.DropColumn(
                name: "LifecycleBatchId",
                table: "RepairDocumentLogs");

            migrationBuilder.DropColumn(
                name: "LifecycleBatchId",
                table: "ItemMovementHistories");

            migrationBuilder.DropColumn(
                name: "LifecycleBatchId",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "LifecycleBatchId",
                table: "BorrowDocumentLogs");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "RepairDocumentLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "BorrowDocumentLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
