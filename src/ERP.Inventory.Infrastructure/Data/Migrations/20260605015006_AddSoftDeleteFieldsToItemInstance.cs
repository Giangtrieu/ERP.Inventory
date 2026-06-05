using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Inventory.Infrastructure.Data.Migrations
{
    public partial class AddSoftDeleteFieldsToItemInstance : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ItemInstances_Barcode",
                table: "ItemInstances");

            migrationBuilder.DropIndex(
                name: "IX_ItemInstances_ItemId_SerialNumber",
                table: "ItemInstances");

            migrationBuilder.AddColumn<bool>(
                name: "CanRestore",
                table: "ItemInstances",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "DeleteReason",
                table: "ItemInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeleteSourceDocumentId",
                table: "ItemInstances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeleteSourceDocumentType",
                table: "ItemInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ItemInstances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserCode",
                table: "ItemInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeletedByUserId",
                table: "ItemInstances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserName",
                table: "ItemInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ItemInstances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RestoreReason",
                table: "ItemInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RestoredAt",
                table: "ItemInstances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestoredByUserCode",
                table: "ItemInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RestoredByUserId",
                table: "ItemInstances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeleteReason",
                table: "CurrentItemLocations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "CurrentItemLocations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CurrentItemLocations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ItemInstances_Barcode",
                table: "ItemInstances",
                column: "Barcode",
                filter: "[Barcode] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ItemInstances_ItemId_SerialNumber",
                table: "ItemInstances",
                columns: new[] { "ItemId", "SerialNumber" },
                unique: true,
                filter: "[SerialNumber] IS NOT NULL AND [IsDeleted] = 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ItemInstances_Barcode",
                table: "ItemInstances");

            migrationBuilder.DropIndex(
                name: "IX_ItemInstances_ItemId_SerialNumber",
                table: "ItemInstances");

            migrationBuilder.DropColumn(
                name: "CanRestore",
                table: "ItemInstances");

            migrationBuilder.DropColumn(
                name: "DeleteReason",
                table: "ItemInstances");

            migrationBuilder.DropColumn(
                name: "DeleteSourceDocumentId",
                table: "ItemInstances");

            migrationBuilder.DropColumn(
                name: "DeleteSourceDocumentType",
                table: "ItemInstances");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ItemInstances");

            migrationBuilder.DropColumn(
                name: "DeletedByUserCode",
                table: "ItemInstances");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "ItemInstances");

            migrationBuilder.DropColumn(
                name: "DeletedByUserName",
                table: "ItemInstances");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ItemInstances");

            migrationBuilder.DropColumn(
                name: "RestoreReason",
                table: "ItemInstances");

            migrationBuilder.DropColumn(
                name: "RestoredAt",
                table: "ItemInstances");

            migrationBuilder.DropColumn(
                name: "RestoredByUserCode",
                table: "ItemInstances");

            migrationBuilder.DropColumn(
                name: "RestoredByUserId",
                table: "ItemInstances");

            migrationBuilder.DropColumn(
                name: "DeleteReason",
                table: "CurrentItemLocations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "CurrentItemLocations");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CurrentItemLocations");

            migrationBuilder.CreateIndex(
                name: "IX_ItemInstances_Barcode",
                table: "ItemInstances",
                column: "Barcode",
                filter: "[Barcode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ItemInstances_ItemId_SerialNumber",
                table: "ItemInstances",
                columns: new[] { "ItemId", "SerialNumber" },
                unique: true,
                filter: "[SerialNumber] IS NOT NULL");
        }
    }
}
