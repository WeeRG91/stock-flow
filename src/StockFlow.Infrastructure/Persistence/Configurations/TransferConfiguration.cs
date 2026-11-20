using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockFlow.Domain.Entities;

namespace StockFlow.Infrastructure.Persistence.Configurations
{
    public sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
    {
        public void Configure(EntityTypeBuilder<Transfer> builder)
        {
            builder.ToTable("Transfers", table =>
            {
                table.HasCheckConstraint(
                    "CK_Transfers_DifferentWarehouses",
                    "\"SourceWarehouseId\" <> \"DestinationWarehouseId\"");
            });

            builder.HasKey(transfer => transfer.Id);
            builder.Property(transfer => transfer.Id).ValueGeneratedNever();

            builder.Property(transfer => transfer.SourceWarehouseId).IsRequired();
            builder.Property(transfer => transfer.SourceWarehouseCode).IsRequired();
            builder.Property(transfer => transfer.SourceWarehouseName).IsRequired();

            builder.Property(transfer => transfer.DestinationWarehouseId).IsRequired();
            builder.Property(transfer => transfer.DestinationWarehouseCode).IsRequired();
            builder.Property(transfer => transfer.DestinationWarehouseName).IsRequired();

            builder.Property(transfer => transfer.CreatedAt).IsRequired();
            builder.Property(transfer => transfer.CreatedByUserId).IsRequired();
            builder.Property(transfer => transfer.Status).IsRequired();

            builder.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(transfer => transfer.SourceWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(transfer => transfer.DestinationWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(transfer => transfer.Lines)
                .WithOne()
                .HasForeignKey("TransferId")
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            builder.Navigation(transfer => transfer.Lines)
                .HasField("_lines")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.Ignore(transfer => transfer.PendingReceipts);

            builder.Property<Guid>("Version")
                .IsRequired()
                .IsConcurrencyToken()
                .ValueGeneratedNever();
        }
    }
}
