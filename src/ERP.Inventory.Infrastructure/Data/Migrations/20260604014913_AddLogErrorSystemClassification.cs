using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Inventory.Infrastructure.Data.Migrations
{
    public partial class AddLogErrorSystemClassification : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "LogErrorSystem",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "DurationMs",
                table: "LogErrorSystem",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExceptionType",
                table: "LogErrorSystem",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Severity",
                table: "LogErrorSystem",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SqlErrorNumber",
                table: "LogErrorSystem",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusCode",
                table: "LogErrorSystem",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalMessage",
                table: "LogErrorSystem",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogErrorSystem_Category",
                table: "LogErrorSystem",
                column: "Category");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LogErrorSystem_Category",
                table: "LogErrorSystem");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "LogErrorSystem");

            migrationBuilder.DropColumn(
                name: "DurationMs",
                table: "LogErrorSystem");

            migrationBuilder.DropColumn(
                name: "ExceptionType",
                table: "LogErrorSystem");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "LogErrorSystem");

            migrationBuilder.DropColumn(
                name: "SqlErrorNumber",
                table: "LogErrorSystem");

            migrationBuilder.DropColumn(
                name: "StatusCode",
                table: "LogErrorSystem");

            migrationBuilder.DropColumn(
                name: "TechnicalMessage",
                table: "LogErrorSystem");
        }
    }
}
