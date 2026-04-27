using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Inventory.Infrastructure.Data.Migrations
{
    [DbContext(typeof(InventoryDbContext))]
    [Migration("20260427090000_AddBorrowLineTargetBin")]
    public partial class AddBorrowLineTargetBin : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TargetBinLocationId",
                table: "BorrowDocumentLines",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BorrowDocumentLines_TargetBinLocationId",
                table: "BorrowDocumentLines",
                column: "TargetBinLocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_BorrowDocumentLines_BinLocations_TargetBinLocationId",
                table: "BorrowDocumentLines",
                column: "TargetBinLocationId",
                principalTable: "BinLocations",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BorrowDocumentLines_BinLocations_TargetBinLocationId",
                table: "BorrowDocumentLines");

            migrationBuilder.DropIndex(
                name: "IX_BorrowDocumentLines_TargetBinLocationId",
                table: "BorrowDocumentLines");

            migrationBuilder.DropColumn(
                name: "TargetBinLocationId",
                table: "BorrowDocumentLines");
        }
    }
}
