using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Inventory.Infrastructure.Data.Migrations
{
    public partial class AddReconciliationAudit : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReferenceListHeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceListHeaders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferenceListHeaders_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReferenceListId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    SessionStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalRef = table.Column<int>(type: "int", nullable: false),
                    TotalErp = table.Column<int>(type: "int", nullable: false),
                    MatchedCount = table.Column<int>(type: "int", nullable: false),
                    ERPOnlyCount = table.Column<int>(type: "int", nullable: false),
                    RefOnlyCount = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReconciliationSessions_ReferenceListHeaders_ReferenceListId",
                        column: x => x.ReferenceListId,
                        principalTable: "ReferenceListHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReconciliationSessions_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReferenceListItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReferenceListId = table.Column<int>(type: "int", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ItemInstanceId = table.Column<int>(type: "int", nullable: true),
                    ItemId = table.Column<int>(type: "int", nullable: true),
                    ResolvedItemName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImportedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferenceListItems_ItemInstances_ItemInstanceId",
                        column: x => x.ItemInstanceId,
                        principalTable: "ItemInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReferenceListItems_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReferenceListItems_ReferenceListHeaders_ReferenceListId",
                        column: x => x.ReferenceListId,
                        principalTable: "ReferenceListHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ItemInstanceId = table.Column<int>(type: "int", nullable: true),
                    ResolvedItemName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResultType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ErpStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErpLocationText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefListItemId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReconciliationResults_ItemInstances_ItemInstanceId",
                        column: x => x.ItemInstanceId,
                        principalTable: "ItemInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReconciliationResults_ReconciliationSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ReconciliationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReconciliationResults_ReferenceListItems_RefListItemId",
                        column: x => x.RefListItemId,
                        principalTable: "ReferenceListItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationResults_ItemInstanceId",
                table: "ReconciliationResults",
                column: "ItemInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationResults_RefListItemId",
                table: "ReconciliationResults",
                column: "RefListItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationResults_SessionId",
                table: "ReconciliationResults",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationResults_SessionId_ResultType",
                table: "ReconciliationResults",
                columns: new[] { "SessionId", "ResultType" });

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationSessions_ReferenceListId",
                table: "ReconciliationSessions",
                column: "ReferenceListId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationSessions_SessionNo",
                table: "ReconciliationSessions",
                column: "SessionNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationSessions_WarehouseId",
                table: "ReconciliationSessions",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceListHeaders_ListCode",
                table: "ReferenceListHeaders",
                column: "ListCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceListHeaders_WarehouseId",
                table: "ReferenceListHeaders",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceListItems_ItemId",
                table: "ReferenceListItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceListItems_ItemInstanceId",
                table: "ReferenceListItems",
                column: "ItemInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceListItems_ReferenceListId_ItemCode_SerialNumber",
                table: "ReferenceListItems",
                columns: new[] { "ReferenceListId", "ItemCode", "SerialNumber" },
                unique: true,
                filter: "[SerialNumber] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReconciliationResults");

            migrationBuilder.DropTable(
                name: "ReconciliationSessions");

            migrationBuilder.DropTable(
                name: "ReferenceListItems");

            migrationBuilder.DropTable(
                name: "ReferenceListHeaders");
        }
    }
}
