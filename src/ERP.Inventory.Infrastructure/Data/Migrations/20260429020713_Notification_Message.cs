using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Inventory.Infrastructure.Data.Migrations
{
    public partial class Notification_Message : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Message",
                table: "Notifications",
                newName: "Message_Zh");

            migrationBuilder.AddColumn<string>(
                name: "Message_En",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Message_Vi",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.DropColumn(
                name: "Message_En",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Message_Vi",
                table: "Notifications");

            migrationBuilder.RenameColumn(
                name: "Message_Zh",
                table: "Notifications",
                newName: "Message");
        }
    }
}
