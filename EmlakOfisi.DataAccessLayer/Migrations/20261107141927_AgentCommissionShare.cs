using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmlakOfisi.DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AgentCommissionShare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AgentShareAmount",
                table: "Transactions",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AgentShareRate",
                table: "Transactions",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionSharePercent",
                table: "AspNetUsers",
                type: "decimal(5,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgentShareAmount",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AgentShareRate",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CommissionSharePercent",
                table: "AspNetUsers");
        }
    }
}
