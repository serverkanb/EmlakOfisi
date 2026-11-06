using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmlakOfisi.DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class PropertyTurkishCollation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Properties",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                collation: "Turkish_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Neighborhood",
                table: "Properties",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "Turkish_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "District",
                table: "Properties",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                collation: "Turkish_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Properties",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                collation: "Turkish_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Properties",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldCollation: "Turkish_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "Neighborhood",
                table: "Properties",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldCollation: "Turkish_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "District",
                table: "Properties",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldCollation: "Turkish_CI_AS");

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Properties",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldCollation: "Turkish_CI_AS");
        }
    }
}
