using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Inventory.Infrastructure.Data.Migrations
{
    public partial class UpdateDatabase : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<string>(
                name: "DepartmentOwner",
                table: "InboundDocuments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PartyDepartment",
                table: "InboundDocuments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PartyPhone",
                table: "InboundDocuments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ReceiverId",
                table: "InboundDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BorrowDocumentLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BorrowDocumentId = table.Column<int>(type: "int", nullable: false),
                    ItemInstanceId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OldStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Borrower = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BorrowDepartment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BorrowerPhone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DepartmentOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OldLocationText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewLocationText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PerformedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BorrowDocumentLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BorrowDocumentLogs_BorrowDocuments_BorrowDocumentId",
                        column: x => x.BorrowDocumentId,
                        principalTable: "BorrowDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BorrowDocumentLogs_ItemInstances_ItemInstanceId",
                        column: x => x.ItemInstanceId,
                        principalTable: "ItemInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InboundDocumentLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InboundDocumentId = table.Column<int>(type: "int", nullable: false),
                    ItemInstanceId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OldStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Receiver = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceiverPhone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceiverDepartment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DepartmentOwner = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OldLocationText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewLocationText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PerformedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundDocumentLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InboundDocumentLogs_InboundDocuments_InboundDocumentId",
                        column: x => x.InboundDocumentId,
                        principalTable: "InboundDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InboundDocumentLogs_ItemInstances_ItemInstanceId",
                        column: x => x.ItemInstanceId,
                        principalTable: "ItemInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboundDocuments_ReceiverId",
                table: "InboundDocuments",
                column: "ReceiverId");

            migrationBuilder.CreateIndex(
                name: "IX_BorrowDocumentLogs_BorrowDocumentId",
                table: "BorrowDocumentLogs",
                column: "BorrowDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_BorrowDocumentLogs_ItemInstanceId_Timestamp",
                table: "BorrowDocumentLogs",
                columns: new[] { "ItemInstanceId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_InboundDocumentLogs_InboundDocumentId",
                table: "InboundDocumentLogs",
                column: "InboundDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_InboundDocumentLogs_ItemInstanceId_Timestamp",
                table: "InboundDocumentLogs",
                columns: new[] { "ItemInstanceId", "Timestamp" });

            migrationBuilder.AddForeignKey(
                name: "FK_InboundDocuments_ExternalParties_ReceiverId",
                table: "InboundDocuments",
                column: "ReceiverId",
                principalTable: "ExternalParties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InboundDocuments_ExternalParties_ReceiverId",
                table: "InboundDocuments");

            migrationBuilder.DropTable(
                name: "BorrowDocumentLogs");

            migrationBuilder.DropTable(
                name: "InboundDocumentLogs");

            migrationBuilder.DropIndex(
                name: "IX_InboundDocuments_ReceiverId",
                table: "InboundDocuments");

            migrationBuilder.DropColumn(
                name: "DepartmentOwner",
                table: "InboundDocuments");

            migrationBuilder.DropColumn(
                name: "PartyDepartment",
                table: "InboundDocuments");

            migrationBuilder.DropColumn(
                name: "PartyPhone",
                table: "InboundDocuments");

            migrationBuilder.DropColumn(
                name: "ReceiverId",
                table: "InboundDocuments");
        }
    }
}
