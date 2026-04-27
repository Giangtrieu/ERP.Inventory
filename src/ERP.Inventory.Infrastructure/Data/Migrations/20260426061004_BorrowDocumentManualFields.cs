using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Inventory.Infrastructure.Data.Migrations
{
    public partial class BorrowDocumentManualFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BorrowDepartment",
                table: "BorrowDocuments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BorrowerPhone",
                table: "BorrowDocuments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DepartmentOwner",
                table: "BorrowDocuments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BorrowDepartment",
                table: "BorrowDocuments");

            migrationBuilder.DropColumn(
                name: "BorrowerPhone",
                table: "BorrowDocuments");

            migrationBuilder.DropColumn(
                name: "DepartmentOwner",
                table: "BorrowDocuments");
        }
    }
}
