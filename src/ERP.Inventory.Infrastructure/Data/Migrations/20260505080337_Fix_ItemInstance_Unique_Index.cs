using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Inventory.Infrastructure.Data.Migrations
{
    public partial class Fix_ItemInstance_Unique_Index : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ItemInstances_SerialNumber",
                table: "ItemInstances");

            migrationBuilder.DropIndex(
                name: "IX_ItemInstances_Barcode",
                table: "ItemInstances");

            migrationBuilder.CreateIndex(
                name: "IX_ItemInstances_ItemId_SerialNumber",
                table: "ItemInstances",
                columns: new[] { "ItemId", "SerialNumber" },
                unique: true,
                filter: "[SerialNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
               name: "IX_ItemInstances_Barcode",
               table: "ItemInstances",
               column: "Barcode",
               filter: "[Barcode] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            
            migrationBuilder.DropIndex(
                name: "IX_ItemInstances_ItemId_SerialNumber",
                table: "ItemInstances");

            migrationBuilder.DropIndex(
                name: "IX_ItemInstances_Barcode",
                table: "ItemInstances");

            

            migrationBuilder.CreateIndex(
                name: "IX_ItemInstances_SerialNumber",
                table: "ItemInstances",
                column: "SerialNumber",
                unique: true,
                filter: "[SerialNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ItemInstances_Barcode",
                table: "ItemInstances",
                column: "Barcode",
                unique: true,
                filter: "[Barcode] IS NOT NULL");
        }
    }
}
