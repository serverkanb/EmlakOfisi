using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmlakOfisi.DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class PropertyInterests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PropertyInterests",
                columns: table => new
                {
                    PropertyInterestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropertyId = table.Column<int>(type: "int", nullable: false),
                    ContactId = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyInterests", x => x.PropertyInterestId);
                    table.ForeignKey(
                        name: "FK_PropertyInterests_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyInterests_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "ContactId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyInterests_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "PropertyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyInterests_ContactId",
                table: "PropertyInterests",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyInterests_CreatedByUserId",
                table: "PropertyInterests",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyInterests_PropertyId_ContactId",
                table: "PropertyInterests",
                columns: new[] { "PropertyId", "ContactId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropertyInterests");
        }
    }
}
