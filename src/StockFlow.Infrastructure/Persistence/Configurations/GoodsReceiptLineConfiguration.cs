using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockFlow.Domain.Entities;

namespace StockFlow.Infrastructure.Persistence.Configurations
{
    public sealed class GoodsReceiptLineConfiguration
        : IEntityTypeConfiguration<GoodsReceiptLine>
    {
        public void Configure(EntityTypeBuilder<GoodsReceiptLine> builder)
        {
            builder.ToTable("GoodsReceiptLines", table =>
            {
                table.HasCheckConstraint(
                    "CK_GoodsReceiptLines_ReceivedQuantity_Positive",
                    "\"ReceivedQuantity\" > 0");
            });

            builder.Property<Guid>("GoodsReceiptId");
            builder.Property(line => line.PurchaseOrderLineId).IsRequired();
            builder.Property(line => line.ReceivedQuantity).IsRequired();

            builder.HasKey(
                "GoodsReceiptId",
                nameof(GoodsReceiptLine.PurchaseOrderLineId));

            builder.HasOne<PurchaseOrderLine>()
                .WithMany()
                .HasForeignKey(line => line.PurchaseOrderLineId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}