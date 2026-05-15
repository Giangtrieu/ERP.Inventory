using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Inventory.Infrastructure.Data.Migrations
{
    public partial class AddDocumentLogsAndSessionStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<string>(
                name: "SessionStatus",
                table: "InventoryCheckDocuments",
                type: "nvarchar(20)",
                nullable: false,
                defaultValue: "Draft");

            // Backfill: các phiếu cũ đã ghi sổ (PostedAt IS NOT NULL) → "Finalized"
            migrationBuilder.Sql(
                "UPDATE [InventoryCheckDocuments] SET [SessionStatus] = 'Finalized' WHERE [PostedAt] IS NOT NULL");

            migrationBuilder.CreateTable(
                name: "AdjustmentDocumentLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdjustmentDocumentId = table.Column<int>(type: "int", nullable: false),
                    ItemInstanceId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OldStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OldLocationText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewLocationText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PerformedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdjustmentDocumentLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdjustmentDocumentLogs_AdjustmentDocuments_AdjustmentDocumentId",
                        column: x => x.AdjustmentDocumentId,
                        principalTable: "AdjustmentDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdjustmentDocumentLogs_ItemInstances_ItemInstanceId",
                        column: x => x.ItemInstanceId,
                        principalTable: "ItemInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RepairDocumentLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RepairDocumentId = table.Column<int>(type: "int", nullable: false),
                    ItemInstanceId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OldStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RepairVendorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExternalLocation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OldLocationText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewLocationText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RepairResultNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PerformedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairDocumentLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepairDocumentLogs_ItemInstances_ItemInstanceId",
                        column: x => x.ItemInstanceId,
                        principalTable: "ItemInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RepairDocumentLogs_RepairDocuments_RepairDocumentId",
                        column: x => x.RepairDocumentId,
                        principalTable: "RepairDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdjustmentDocumentLogs_AdjustmentDocumentId",
                table: "AdjustmentDocumentLogs",
                column: "AdjustmentDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AdjustmentDocumentLogs_ItemInstanceId_Timestamp",
                table: "AdjustmentDocumentLogs",
                columns: new[] { "ItemInstanceId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_RepairDocumentLogs_ItemInstanceId_Timestamp",
                table: "RepairDocumentLogs",
                columns: new[] { "ItemInstanceId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_RepairDocumentLogs_RepairDocumentId",
                table: "RepairDocumentLogs",
                column: "RepairDocumentId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdjustmentDocumentLogs");

            migrationBuilder.DropTable(
                name: "RepairDocumentLogs");

            migrationBuilder.DropColumn(
                name: "SessionStatus",
                table: "InventoryCheckDocuments");

        }
    }
}
