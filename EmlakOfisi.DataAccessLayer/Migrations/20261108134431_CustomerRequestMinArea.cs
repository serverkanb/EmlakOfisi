using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmlakOfisi.DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class CustomerRequestMinArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinArea",
                table: "CustomerRequests",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinArea",
                table: "CustomerRequests");
        }
    }
}
