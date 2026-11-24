using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransferReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransferReceipts_Transfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "Transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransferReceiptLines",
                columns: table => new
                {
                    TransferLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferReceiptLines", x => new { x.TransferReceiptId, x.TransferLineId });
                    table.CheckConstraint("CK_TransferReceiptLines_ReceivedQuantity_Positive", "\"ReceivedQuantity\" > 0");
                    table.ForeignKey(
                        name: "FK_TransferReceiptLines_TransferLines_TransferLineId",
                        column: x => x.TransferLineId,
                        principalTable: "TransferLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferReceiptLines_TransferReceipts_TransferReceiptId",
                        column: x => x.TransferReceiptId,
                        principalTable: "TransferReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransferReceiptLines_TransferLineId",
                table: "TransferReceiptLines",
                column: "TransferLineId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferReceipts_TransferId",
                table: "TransferReceipts",
                column: "TransferId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransferReceiptLines");

            migrationBuilder.DropTable(
                name: "TransferReceipts");
        }
    }
}
