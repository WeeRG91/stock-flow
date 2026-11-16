using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockFlow.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace StockFlow.Infrastructure.Persistence.Configurations
{
    public sealed class GoodsReceiptConfiguration : IEntityTypeConfiguration<GoodsReceipt>
    {
        public void Configure(EntityTypeBuilder<GoodsReceipt> builder)
        {
            builder.ToTable("GoodsReceipts");
            builder.HasKey(receipt => receipt.Id);

            builder.Property(receipt => receipt.Id)
                .ValueGeneratedNever();

            builder.Property(receipt => receipt.PurchaseOrderId).IsRequired();
            builder.Property(receipt => receipt.ConfirmedAt).IsRequired();
            builder.Property(receipt => receipt.ConfirmedByUserId).IsRequired();

            builder.HasOne<PurchaseOrder>()
                .WithMany()
                .HasForeignKey(receipt => receipt.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(receipt => receipt.Lines)
                .WithOne()
                .HasForeignKey("GoodsReceiptId")
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            builder.Navigation(receipt => receipt.Lines)
                .HasField("_lines")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
