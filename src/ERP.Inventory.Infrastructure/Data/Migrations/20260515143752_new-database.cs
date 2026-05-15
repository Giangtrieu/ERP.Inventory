using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Inventory.Infrastructure.Data.Migrations
{
    public partial class newdatabase : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiveResult",
                table: "RepairDocuments");

            migrationBuilder.AddColumn<bool>(
                name: "IsReturned",
                table: "RepairDocumentLines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OwnerName",
                table: "ItemInstances",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReturned",
                table: "RepairDocumentLines");

            migrationBuilder.DropColumn(
                name: "OwnerName",
                table: "ItemInstances");

            migrationBuilder.AddColumn<int>(
                name: "ReceiveResult",
                table: "RepairDocuments",
                type: "int",
                nullable: true);
        }
    }
}
