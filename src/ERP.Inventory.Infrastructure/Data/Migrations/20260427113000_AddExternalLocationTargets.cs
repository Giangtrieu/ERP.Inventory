using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Inventory.Infrastructure.Data.Migrations
{
    [DbContext(typeof(InventoryDbContext))]
    [Migration("20260427113000_AddExternalLocationTargets")]
    public partial class AddExternalLocationTargets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalLocationText",
                table: "CurrentItemLocations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetExternalLocation",
                table: "RepairDocumentLines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetExternalLocation",
                table: "BorrowDocumentLines",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalLocationText",
                table: "CurrentItemLocations");

            migrationBuilder.DropColumn(
                name: "TargetExternalLocation",
                table: "RepairDocumentLines");

            migrationBuilder.DropColumn(
                name: "TargetExternalLocation",
                table: "BorrowDocumentLines");
        }
    }
}
