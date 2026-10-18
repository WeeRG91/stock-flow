using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryReorderThreshold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReorderThreshold",
                table: "Inventories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Inventories_ReorderThreshold_Positive",
                table: "Inventories",
                sql: "\"ReorderThreshold\" IS NULL OR \"ReorderThreshold\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventories_ReorderThreshold_Positive",
                table: "Inventories");

            migrationBuilder.DropColumn(
                name: "ReorderThreshold",
                table: "Inventories");
        }
    }
}
