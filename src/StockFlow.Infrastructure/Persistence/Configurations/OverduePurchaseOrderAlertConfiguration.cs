using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockFlow.Domain.Entities;

namespace StockFlow.Infrastructure.Persistence.Configurations
{
    public sealed class OverduePurchaseOrderAlertConfiguration : IEntityTypeConfiguration<OverduePurchaseOrderAlert>
    {
        public void Configure(EntityTypeBuilder<OverduePurchaseOrderAlert> builder)
        {
            builder.ToTable("OverduePurchaseOrderAlerts", table =>
            {
                table.HasCheckConstraint(
                    "CK_OverduePurchaseOrderAlerts_ResolutionAfterDetection",
                    "\"ResolvedAt\" IS NULL OR \"ResolvedAt\" >= \"DetectedAt\"");
            });

            builder.HasKey(alert => alert.Id);
            builder.Property(alert => alert.Id).ValueGeneratedNever();
            builder.Property(alert => alert.PurchaseOrderId).IsRequired();

            builder.Property(alert => alert.DetectedAt)
                .HasColumnType("timestamp with time zone")
                .IsRequired();

            builder.Property(alert => alert.ResolvedAt)
                .HasColumnType("timestamp with time zone")
                .IsRequired(false);

            builder.Ignore(alert => alert.IsActive);

            builder.HasIndex(alert => alert.PurchaseOrderId)
                .IsUnique()
                .HasDatabaseName("IX_OverduePurchaseOrderAlerts_PurchaseOrderId");

            builder.HasOne<PurchaseOrder>()
                .WithMany()
                .HasForeignKey(alert => alert.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
