using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Inventory.Infrastructure.Data.Migrations
{
    public partial class AddLogErrorSystem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LogErrorSystem",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ErrorCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RequestPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HttpMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Module = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InnerException = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClientIp = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Browser = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogErrorSystem", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogErrorSystem_CreatedAt",
                table: "LogErrorSystem",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LogErrorSystem_ErrorCode",
                table: "LogErrorSystem",
                column: "ErrorCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogErrorSystem_IsResolved",
                table: "LogErrorSystem",
                column: "IsResolved");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogErrorSystem");
        }
    }
}
