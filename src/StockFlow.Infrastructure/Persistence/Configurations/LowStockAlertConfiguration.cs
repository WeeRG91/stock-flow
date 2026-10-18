using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockFlow.Domain.Entities;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Persistence.Configurations
{
    public sealed class LowStockAlertConfiguration : IEntityTypeConfiguration<LowStockAlert>
    {
        public void Configure(EntityTypeBuilder<LowStockAlert> builder)
        {
            builder.ToTable("LowStockAlerts", table =>
            {
                table.HasCheckConstraint(
                    "CK_LowStockAlerts_ResolutionAfterDetection",
                    "\"ResolvedAt\" IS NULL OR \"ResolvedAt\" >= \"DetectedAt\"");
            });

            builder.HasKey(alert => alert.Id);

            builder.Property(alert => alert.Id)
                .ValueGeneratedNever();

            builder.Property(alert => alert.InventoryId)
                .IsRequired();

            builder.Property(alert => alert.DetectedAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            builder.Property(alert => alert.ResolvedAt)
                .IsRequired(false)
                .HasColumnType("timestamp with time zone");

            builder.Ignore(alert => alert.IsActive);

            builder.HasIndex(alert => alert.InventoryId)
                .IsUnique();

            builder.HasOne<InventoryEntity>()
                .WithMany()
                .HasForeignKey(alert => alert.InventoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
