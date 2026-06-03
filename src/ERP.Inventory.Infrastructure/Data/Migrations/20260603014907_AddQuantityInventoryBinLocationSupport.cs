using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Inventory.Infrastructure.Data.Migrations
{
    public partial class AddQuantityInventoryBinLocationSupport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BinCode",
                table: "QuantityInventoryTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "BinLocationId",
                table: "QuantityInventoryTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BinLocationId",
                table: "QuantityInventoryDocumentLines",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "QuantityStockLocationBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    BinLocationId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuantityStockLocationBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuantityStockLocationBalances_BinLocations_BinLocationId",
                        column: x => x.BinLocationId,
                        principalTable: "BinLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantityStockLocationBalances_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuantityStockLocationBalances_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuantityInventoryTransactions_BinLocationId",
                table: "QuantityInventoryTransactions",
                column: "BinLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantityInventoryDocumentLines_BinLocationId",
                table: "QuantityInventoryDocumentLines",
                column: "BinLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantityStockLocationBalances_BinLocationId",
                table: "QuantityStockLocationBalances",
                column: "BinLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantityStockLocationBalances_ItemId",
                table: "QuantityStockLocationBalances",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_QuantityStockLocationBalances_WarehouseId_BinLocationId_ItemId_Status",
                table: "QuantityStockLocationBalances",
                columns: new[] { "WarehouseId", "BinLocationId", "ItemId", "Status" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_QuantityInventoryDocumentLines_BinLocations_BinLocationId",
                table: "QuantityInventoryDocumentLines",
                column: "BinLocationId",
                principalTable: "BinLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuantityInventoryTransactions_BinLocations_BinLocationId",
                table: "QuantityInventoryTransactions",
                column: "BinLocationId",
                principalTable: "BinLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuantityInventoryDocumentLines_BinLocations_BinLocationId",
                table: "QuantityInventoryDocumentLines");

            migrationBuilder.DropForeignKey(
                name: "FK_QuantityInventoryTransactions_BinLocations_BinLocationId",
                table: "QuantityInventoryTransactions");

            migrationBuilder.DropTable(
                name: "QuantityStockLocationBalances");

            migrationBuilder.DropIndex(
                name: "IX_QuantityInventoryTransactions_BinLocationId",
                table: "QuantityInventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_QuantityInventoryDocumentLines_BinLocationId",
                table: "QuantityInventoryDocumentLines");

            migrationBuilder.DropColumn(
                name: "BinCode",
                table: "QuantityInventoryTransactions");

            migrationBuilder.DropColumn(
                name: "BinLocationId",
                table: "QuantityInventoryTransactions");

            migrationBuilder.DropColumn(
                name: "BinLocationId",
                table: "QuantityInventoryDocumentLines");
        }
    }
}
