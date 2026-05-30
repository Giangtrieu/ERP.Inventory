using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Inventory.Infrastructure.Data.Migrations
{
    public partial class logQuantity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReceiverCode",
                table: "QuantityInventoryTransactions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReceiverName",
                table: "QuantityInventoryTransactions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReceiverPhone",
                table: "QuantityInventoryTransactions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SenderCode",
                table: "QuantityInventoryTransactions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SenderName",
                table: "QuantityInventoryTransactions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SenderPhone",
                table: "QuantityInventoryTransactions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiverCode",
                table: "QuantityInventoryTransactions");

            migrationBuilder.DropColumn(
                name: "ReceiverName",
                table: "QuantityInventoryTransactions");

            migrationBuilder.DropColumn(
                name: "ReceiverPhone",
                table: "QuantityInventoryTransactions");

            migrationBuilder.DropColumn(
                name: "SenderCode",
                table: "QuantityInventoryTransactions");

            migrationBuilder.DropColumn(
                name: "SenderName",
                table: "QuantityInventoryTransactions");

            migrationBuilder.DropColumn(
                name: "SenderPhone",
                table: "QuantityInventoryTransactions");
        }
    }
}
