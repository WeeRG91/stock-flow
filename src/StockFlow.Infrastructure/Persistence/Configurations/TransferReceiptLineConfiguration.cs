using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockFlow.Domain.Entities;

namespace StockFlow.Infrastructure.Persistence.Configurations
{
    public sealed class TransferReceiptLineConfiguration : IEntityTypeConfiguration<TransferReceiptLine>
    {
        public void Configure(EntityTypeBuilder<TransferReceiptLine> builder)
        {
            builder.ToTable("TransferReceiptLines", table =>
            {
                table.HasCheckConstraint(
                    "CK_TransferReceiptLines_ReceivedQuantity_Positive",
                    "\"ReceivedQuantity\" > 0");
            });

            builder.Property<Guid>("TransferReceiptId")
                .IsRequired();

            builder.Property(line => line.TransferLineId)
                .IsRequired();

            builder.Property(line => line.ReceivedQuantity)
                .IsRequired();

            builder.HasKey(
                "TransferReceiptId",
                nameof(TransferReceiptLine.TransferLineId));

            builder.HasOne<TransferLine>()
                .WithMany()
                .HasForeignKey(line => line.TransferLineId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
