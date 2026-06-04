using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Inventory.Infrastructure.Data.Migrations
{
    public partial class AddBinLocationUsageType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UsageType",
                table: "BinLocations",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "LocationTracked");

            migrationBuilder.CreateIndex(
                name: "IX_BinLocations_WarehouseId_UsageType",
                table: "BinLocations",
                columns: new[] { "WarehouseId", "UsageType" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BinLocations_WarehouseId_UsageType",
                table: "BinLocations");

            migrationBuilder.DropColumn(
                name: "UsageType",
                table: "BinLocations");
        }
    }
}
