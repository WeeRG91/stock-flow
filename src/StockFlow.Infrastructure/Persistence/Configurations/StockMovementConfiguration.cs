using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockFlow.Domain.Entities;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Persistence.Configurations
{
    public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
    {
        public void Configure(EntityTypeBuilder<StockMovement> builder)
        {
            builder.ToTable("StockMovements", table =>
            {
                table.HasCheckConstraint(
                    "CK_StockMovements_QuantityChange_NonZero",
                    "\"QuantityChange\" <> 0");

                table.HasCheckConstraint(
                    "CK_StockMovements_TypeAndSign",
                    """
                    ("Type" = 1 AND "QuantityChange" > 0)
                    OR ("Type" = 2 AND "QuantityChange" < 0)
                    OR ("Type" = 3)
                    """);
            });

            builder.HasKey(movement => movement.Id);

            builder.Property(movement => movement.Id)
                .HasField("<Id>k__BackingField")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .ValueGeneratedNever();

            builder.Property(movement => movement.InventoryId)
                .IsRequired();

            builder.Property(movement => movement.QuantityChange)
                .IsRequired();

            builder.Property(movement => movement.Type)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(movement => movement.Reason)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(movement => movement.OccurredAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            builder.HasOne<InventoryEntity>()
                .WithMany()
                .HasForeignKey(movement => movement.InventoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
