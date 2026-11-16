using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoodsReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReceivedQuantity",
                table: "PurchaseOrderLines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "GoodsReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoodsReceipts_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceiptLines",
                columns: table => new
                {
                    PurchaseOrderLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    GoodsReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceiptLines", x => new { x.GoodsReceiptId, x.PurchaseOrderLineId });
                    table.CheckConstraint("CK_GoodsReceiptLines_ReceivedQuantity_Positive", "\"ReceivedQuantity\" > 0");
                    table.ForeignKey(
                        name: "FK_GoodsReceiptLines_GoodsReceipts_GoodsReceiptId",
                        column: x => x.GoodsReceiptId,
                        principalTable: "GoodsReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptLines_PurchaseOrderLines_PurchaseOrderLineId",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseOrderLines_ReceivedQuantity_Valid",
                table: "PurchaseOrderLines",
                sql: "\"ReceivedQuantity\" >= 0 AND \"ReceivedQuantity\" <= \"OrderedQuantity\"");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptLines_PurchaseOrderLineId",
                table: "GoodsReceiptLines",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_PurchaseOrderId",
                table: "GoodsReceipts",
                column: "PurchaseOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoodsReceiptLines");

            migrationBuilder.DropTable(
                name: "GoodsReceipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseOrderLines_ReceivedQuantity_Valid",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "ReceivedQuantity",
                table: "PurchaseOrderLines");
        }
    }
}
