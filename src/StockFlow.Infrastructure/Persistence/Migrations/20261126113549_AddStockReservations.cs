using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "QuantityReserved",
                table: "Inventories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "StockReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReleasedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    FulfilledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FulfilledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReservations", x => x.Id);
                    table.CheckConstraint("CK_StockReservations_Quantity_Positive", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_StockReservations_RequestId_NotEmpty", "\"RequestId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("CK_StockReservations_StatusAndHistory", "(\r\n    \"Status\" = 1\r\n    AND \"ReleasedAt\" IS NULL\r\n    AND \"ReleasedByUserId\" IS NULL\r\n    AND \"FulfilledAt\" IS NULL\r\n    AND \"FulfilledByUserId\" IS NULL\r\n)\r\nOR\r\n(\r\n    \"Status\" = 2\r\n    AND \"ReleasedAt\" IS NOT NULL\r\n    AND \"ReleasedAt\" >= \"CreatedAt\"\r\n    AND \"ReleasedByUserId\" IS NOT NULL\r\n    AND \"ReleasedByUserId\" <> '00000000-0000-0000-0000-000000000000'::uuid\r\n    AND \"FulfilledAt\" IS NULL\r\n    AND \"FulfilledByUserId\" IS NULL\r\n)\r\nOR\r\n(\r\n    \"Status\" = 3\r\n    AND \"FulfilledAt\" IS NOT NULL\r\n    AND \"FulfilledAt\" >= \"CreatedAt\"\r\n    AND \"FulfilledByUserId\" IS NOT NULL\r\n    AND \"FulfilledByUserId\" <> '00000000-0000-0000-0000-000000000000'::uuid\r\n    AND \"ReleasedAt\" IS NULL\r\n    AND \"ReleasedByUserId\" IS NULL\r\n)");
                    table.ForeignKey(
                        name: "FK_StockReservations_Inventories_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "Inventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Inventories_QuantityReserved_Valid",
                table: "Inventories",
                sql: "\"QuantityReserved\" >= 0 AND \"QuantityReserved\" <= \"QuantityOnHand\"");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_InventoryId_Status",
                table: "StockReservations",
                columns: new[] { "InventoryId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_RequestId",
                table: "StockReservations",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockReservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventories_QuantityReserved_Valid",
                table: "Inventories");

            migrationBuilder.DropColumn(
                name: "QuantityReserved",
                table: "Inventories");
        }
    }
}
