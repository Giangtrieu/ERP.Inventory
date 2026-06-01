using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Inventory.Infrastructure.Data.Migrations
{
    public partial class AddPerf002ReportingIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ImportBatchRows_ImportBatchId",
                table: "ImportBatchRows");

            migrationBuilder.AlterColumn<string>(
                name: "DocumentNo",
                table: "ItemMovementHistories",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "DocumentNo",
                table: "InventoryTransactions",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceNo",
                table: "AuditLogs",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemMovementHistories_DocumentNo",
                table: "ItemMovementHistories",
                column: "DocumentNo");

            migrationBuilder.CreateIndex(
                name: "IX_ItemMovementHistories_PerformedAt",
                table: "ItemMovementHistories",
                column: "PerformedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_DocumentNo",
                table: "InventoryTransactions",
                column: "DocumentNo");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_PostedAt",
                table: "InventoryTransactions",
                column: "PostedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatchRows_ImportBatchId_RowNumber",
                table: "ImportBatchRows",
                columns: new[] { "ImportBatchId", "RowNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ReferenceNo",
                table: "AuditLogs",
                column: "ReferenceNo");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ItemMovementHistories_DocumentNo",
                table: "ItemMovementHistories");

            migrationBuilder.DropIndex(
                name: "IX_ItemMovementHistories_PerformedAt",
                table: "ItemMovementHistories");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_DocumentNo",
                table: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_PostedAt",
                table: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_ImportBatchRows_ImportBatchId_RowNumber",
                table: "ImportBatchRows");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ReferenceNo",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<string>(
                name: "DocumentNo",
                table: "ItemMovementHistories",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "DocumentNo",
                table: "InventoryTransactions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceNo",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatchRows_ImportBatchId",
                table: "ImportBatchRows",
                column: "ImportBatchId");
        }
    }
}
