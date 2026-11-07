using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmlakOfisi.DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class PropertyListingUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ListingUrl",
                table: "Properties",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ListingUrl",
                table: "Properties");
        }
    }
}
