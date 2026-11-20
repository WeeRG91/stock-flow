using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockFlow.Domain.Entities;

namespace StockFlow.Infrastructure.Persistence.Configurations
{
    public sealed class TransferLineConfiguration : IEntityTypeConfiguration<TransferLine>
    {
        public void Configure(EntityTypeBuilder<TransferLine> builder)
        {
            builder.ToTable("TransferLines", table =>
            {
                table.HasCheckConstraint(
                    "CK_TransferLines_Quantity_Positive",
                    "\"Quantity\" > 0");

                table.HasCheckConstraint(
                    "CK_TransferLines_DispatchedQuantity_Valid",
                    "\"DispatchedQuantity\" = 0 OR \"DispatchedQuantity\" = \"Quantity\"");

                table.HasCheckConstraint(
                    "CK_TransferLines_ReceivedQuantity_Valid",
                    "\"ReceivedQuantity\" >= 0 AND " +
                    "\"ReceivedQuantity\" <= \"DispatchedQuantity\"");
            });

            builder.HasKey(line => line.Id);

            builder.Property(line => line.Id)
                .HasField("<Id>k__BackingField")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .ValueGeneratedNever();

            builder.Property(line => line.ProductId).IsRequired();
            builder.Property(line => line.ProductSku).IsRequired();
            builder.Property(line => line.ProductName).IsRequired();
            builder.Property(line => line.Quantity).IsRequired();
            builder.Property(line => line.DispatchedQuantity).IsRequired();
            builder.Property(line => line.ReceivedQuantity).IsRequired();

            builder.Ignore(line => line.InTransitQuantity);

            builder.HasIndex("TransferId", nameof(TransferLine.ProductId))
                .IsUnique()
                .HasDatabaseName("IX_TransferLines_TransferId_ProductId");

            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(line => line.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
