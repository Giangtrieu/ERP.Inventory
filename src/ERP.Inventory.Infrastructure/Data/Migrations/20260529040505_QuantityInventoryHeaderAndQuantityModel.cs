using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Inventory.Infrastructure.Data.Migrations
{
    public partial class QuantityInventoryHeaderAndQuantityModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OperatorUserCode",
                table: "QuantityInventoryDocuments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OperatorUserId",
                table: "QuantityInventoryDocuments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OperatorUserName",
                table: "QuantityInventoryDocuments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReceiverCode",
                table: "QuantityInventoryDocuments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReceiverName",
                table: "QuantityInventoryDocuments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReceiverPhone",
                table: "QuantityInventoryDocuments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SenderCode",
                table: "QuantityInventoryDocuments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SenderName",
                table: "QuantityInventoryDocuments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SenderPhone",
                table: "QuantityInventoryDocuments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OperatorUserCode",
                table: "QuantityInventoryDocuments");

            migrationBuilder.DropColumn(
                name: "OperatorUserId",
                table: "QuantityInventoryDocuments");

            migrationBuilder.DropColumn(
                name: "OperatorUserName",
                table: "QuantityInventoryDocuments");

            migrationBuilder.DropColumn(
                name: "ReceiverCode",
                table: "QuantityInventoryDocuments");

            migrationBuilder.DropColumn(
                name: "ReceiverName",
                table: "QuantityInventoryDocuments");

            migrationBuilder.DropColumn(
                name: "ReceiverPhone",
                table: "QuantityInventoryDocuments");

            migrationBuilder.DropColumn(
                name: "SenderCode",
                table: "QuantityInventoryDocuments");

            migrationBuilder.DropColumn(
                name: "SenderName",
                table: "QuantityInventoryDocuments");

            migrationBuilder.DropColumn(
                name: "SenderPhone",
                table: "QuantityInventoryDocuments");
        }
    }
}
