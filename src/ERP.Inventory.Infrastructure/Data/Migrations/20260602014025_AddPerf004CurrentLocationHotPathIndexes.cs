using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Inventory.Infrastructure.Data.Migrations
{
    public partial class AddPerf004CurrentLocationHotPathIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CurrentItemLocations_WarehouseId",
                table: "CurrentItemLocations");

            migrationBuilder.CreateIndex(
                name: "IX_CurrentItemLocations_WarehouseId_BinLocationId",
                table: "CurrentItemLocations",
                columns: new[] { "WarehouseId", "BinLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_CurrentItemLocations_WarehouseId_UpdatedLocationAt",
                table: "CurrentItemLocations",
                columns: new[] { "WarehouseId", "UpdatedLocationAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CurrentItemLocations_WarehouseId_BinLocationId",
                table: "CurrentItemLocations");

            migrationBuilder.DropIndex(
                name: "IX_CurrentItemLocations_WarehouseId_UpdatedLocationAt",
                table: "CurrentItemLocations");

            migrationBuilder.CreateIndex(
                name: "IX_CurrentItemLocations_WarehouseId",
                table: "CurrentItemLocations",
                column: "WarehouseId");
        }
    }
}
