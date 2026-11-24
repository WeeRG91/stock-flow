using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockFlow.Domain.Entities;

namespace StockFlow.Infrastructure.Persistence.Configurations
{
    public sealed class TransferReceiptConfiguration : IEntityTypeConfiguration<TransferReceipt>
    {
        public void Configure(EntityTypeBuilder<TransferReceipt> builder)
        {
            builder.ToTable("TransferReceipts");

            builder.HasKey(receipt => receipt.Id);

            builder.Property(receipt => receipt.Id)
                .ValueGeneratedNever();

            builder.Property(receipt => receipt.TransferId).IsRequired();
            builder.Property(receipt => receipt.ConfirmedAt).IsRequired();
            builder.Property(receipt => receipt.ConfirmedByUserId).IsRequired();

            builder.HasOne<Transfer>()
                .WithMany()
                .HasForeignKey(receipt => receipt.TransferId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(receipt => receipt.Lines)
                .WithOne()
                .HasForeignKey("TransferReceiptId")
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            builder.Navigation(receipt => receipt.Lines)
                .HasField("_lines")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
