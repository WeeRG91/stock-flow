using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockFlow.Domain.Entities;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Persistence.Configurations
{
    public sealed class InventoryConfiguration : IEntityTypeConfiguration<InventoryEntity>
    {
        public void Configure(EntityTypeBuilder<InventoryEntity> builder)
        {
            builder.ToTable("Inventories", table =>
            {
                table.HasCheckConstraint(
                    "CK_Inventories_QuantityOnHand_NonNegative",
                    "\"QuantityOnHand\" >= 0");
            });

            builder.HasKey(inventory => inventory.Id);

            builder.Property(inventory => inventory.Id)
                .ValueGeneratedNever();

            builder.Property(inventory => inventory.ProductId)
                .IsRequired();

            builder.Property(inventory => inventory.WarehouseId)
                .IsRequired();

            builder.Property(inventory => inventory.QuantityOnHand)
                .IsRequired();

            builder.HasIndex(inventory => new
                {
                    inventory.ProductId,
                    inventory.WarehouseId,
                })
                .IsUnique();

            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(inventory => inventory.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(inventory => inventory.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
