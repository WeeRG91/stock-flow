using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockFlow.Domain.Entities;

namespace StockFlow.Infrastructure.Persistence.Configurations
{
    public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
    {
        public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
        {
            builder.ToTable("PurchaseOrders");

            builder.HasKey(order => order.Id);
            builder.Property(order => order.Id)
                .HasField("<Id>k__BackingField")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .ValueGeneratedNever();

            builder.Property(order => order.OrderNumber)
                .IsRequired()
                .HasMaxLength(PurchaseOrder.MaxOrderNumberLength);

            builder.HasIndex(order => order.OrderNumber)
                .IsUnique()
                .HasDatabaseName("IX_PurchaseOrders_OrderNumber");

            builder.Property(order => order.SupplierId).IsRequired();
            builder.Property(order => order.SupplierCode).IsRequired();
            builder.Property(order => order.SupplierName).IsRequired();

            builder.Property(order => order.WarehouseId).IsRequired();
            builder.Property(order => order.WarehouseCode).IsRequired();
            builder.Property(order => order.WarehouseName).IsRequired();

            builder.Property(order => order.CreatedByUserId).IsRequired();
            builder.Property(order => order.CreatedAt).IsRequired();
            builder.Property(order => order.Status).IsRequired();

            builder.Property(order => order.SubmittedAt);
            builder.Property(order => order.ApprovedAt);
            builder.Property(order => order.RejectedAt);

            builder.Property(order => order.ApprovedByUserId).IsRequired(false);
            builder.Property(order => order.RejectedByUserId).IsRequired(false);

            builder.Ignore(order => order.Currency);
            builder.Ignore(order => order.TotalAmount);

            builder.HasMany(order => order.Lines)
                .WithOne()
                .HasForeignKey("PurchaseOrderId")
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            builder.Navigation(order => order.Lines)
                .HasField("_lines")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasOne<Supplier>()
                .WithMany()
                .HasForeignKey(order => order.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(order => order.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property<Guid>("Version")
                .IsRequired()
                .IsConcurrencyToken()
                .ValueGeneratedNever();
        }
    }
}
