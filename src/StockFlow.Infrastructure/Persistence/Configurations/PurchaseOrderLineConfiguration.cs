using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockFlow.Domain.Entities;

namespace StockFlow.Infrastructure.Persistence.Configurations
{
    public sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
    {
        public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
        {
            builder.ToTable("PurchaseOrderLines", table =>
            {
                table.HasCheckConstraint(
                    "CK_PurchaseOrderLines_OrderedQuantity_Positive",
                    "\"OrderedQuantity\" > 0");

                table.HasCheckConstraint(
                    "CK_PurchaseOrderLines_UnitPrice_Valid",
                    "\"UnitPrice\" >= 0 AND " +
                    "\"UnitPrice\" <= 79228162514264337593543950335 AND " +
                    "\"UnitPrice\" = round(\"UnitPrice\", 2)");
            });

            builder.HasKey(line => line.Id);
            builder.Property(line => line.Id)
                .HasField("<Id>k__BackingField")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .ValueGeneratedNever();

            builder.Property(line => line.ProductId).IsRequired();
            builder.Property(line => line.ProductSku).IsRequired();
            builder.Property(line => line.ProductName).IsRequired();
            builder.Property(line => line.OrderedQuantity).IsRequired();

            builder.Property(line => line.UnitPrice)
                .HasColumnType("numeric")
                .IsRequired();

            builder.Ignore(line => line.LineTotal);

            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(line => line.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex("PurchaseOrderId", nameof(PurchaseOrderLine.ProductId))
                .IsUnique()
                .HasDatabaseName("IX_PurchaseOrderLines_PurchaseOrderId_ProductId");
        }
    }
}
