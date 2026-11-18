using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockFlow.Domain.Entities;

namespace StockFlow.Infrastructure.Persistence.Configurations
{
    public sealed class PurchasingNotificationConfiguration : IEntityTypeConfiguration<PurchasingNotification>
    {
        public void Configure(EntityTypeBuilder<PurchasingNotification> builder)
        {
            builder.ToTable("PurchasingNotifications", table =>
            {
                table.HasCheckConstraint(
                    "CK_PurchasingNotifications_ReadAfterCreation",
                    "\"ReadAt\" IS NULL OR \"ReadAt\" >= \"CreatedAt\"");
            });

            builder.HasKey(notification => notification.Id);

            builder.Property(notification => notification.Id)
                .ValueGeneratedNever();

            builder.Property(notification => notification.RecipientUserId).IsRequired();
            builder.Property(notification => notification.PurchaseOrderId).IsRequired();
            builder.Property(notification => notification.Kind).IsRequired();

            builder.Property(notification => notification.CreatedAt)
                .HasColumnType("timestamp with time zone")
                .IsRequired();

            builder.Property(notification => notification.ReadAt)
                .HasColumnType("timestamp with time zone")
                .IsRequired(false);

            builder.HasIndex(notification => new
            {
                notification.RecipientUserId,
                notification.PurchaseOrderId,
                notification.Kind
            }).IsUnique();

            builder.HasOne<PurchaseOrder>()
                .WithMany()
                .HasForeignKey(notification => notification.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
