using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmlakOfisi.DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class TransactionLeaseContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContractContentType",
                table: "Transactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractFileName",
                table: "Transactions",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractOriginalName",
                table: "Transactions",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContractUploadedAt",
                table: "Transactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContractUploadedByUserId",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseEndDate",
                table: "Transactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseStartDate",
                table: "Transactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ContractUploadedByUserId",
                table: "Transactions",
                column: "ContractUploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Type_Status_LeaseEndDate",
                table: "Transactions",
                columns: new[] { "Type", "Status", "LeaseEndDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_AspNetUsers_ContractUploadedByUserId",
                table: "Transactions",
                column: "ContractUploadedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_AspNetUsers_ContractUploadedByUserId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_ContractUploadedByUserId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_Type_Status_LeaseEndDate",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ContractContentType",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ContractFileName",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ContractOriginalName",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ContractUploadedAt",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ContractUploadedByUserId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "LeaseEndDate",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "LeaseStartDate",
                table: "Transactions");
        }
    }
}
