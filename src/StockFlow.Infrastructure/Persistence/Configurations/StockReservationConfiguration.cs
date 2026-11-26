using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockFlow.Domain.Entities;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Persistence.Configurations
{
    public sealed class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
    {
        public void Configure(
            EntityTypeBuilder<StockReservation> builder)
        {
            builder.ToTable("StockReservations", table =>
            {
                table.HasCheckConstraint(
                    "CK_StockReservations_Quantity_Positive",
                    "\"Quantity\" > 0");

                table.HasCheckConstraint(
                    "CK_StockReservations_RequestId_NotEmpty",
                    "\"RequestId\" <> '00000000-0000-0000-0000-000000000000'::uuid");

                table.HasCheckConstraint(
                    "CK_StockReservations_StatusAndHistory",
                    """
                    (
                        "Status" = 1
                        AND "ReleasedAt" IS NULL
                        AND "ReleasedByUserId" IS NULL
                        AND "FulfilledAt" IS NULL
                        AND "FulfilledByUserId" IS NULL
                    )
                    OR
                    (
                        "Status" = 2
                        AND "ReleasedAt" IS NOT NULL
                        AND "ReleasedAt" >= "CreatedAt"
                        AND "ReleasedByUserId" IS NOT NULL
                        AND "ReleasedByUserId" <> '00000000-0000-0000-0000-000000000000'::uuid
                        AND "FulfilledAt" IS NULL
                        AND "FulfilledByUserId" IS NULL
                    )
                    OR
                    (
                        "Status" = 3
                        AND "FulfilledAt" IS NOT NULL
                        AND "FulfilledAt" >= "CreatedAt"
                        AND "FulfilledByUserId" IS NOT NULL
                        AND "FulfilledByUserId" <> '00000000-0000-0000-0000-000000000000'::uuid
                        AND "ReleasedAt" IS NULL
                        AND "ReleasedByUserId" IS NULL
                    )
                    """);
            });

            builder.HasKey(reservation => reservation.Id);

            builder.Property(reservation => reservation.Id)
                .HasField("<Id>k__BackingField")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .ValueGeneratedNever();

            builder.Property(reservation => reservation.InventoryId)
                .IsRequired();

            builder.Property(reservation => reservation.Quantity)
                .IsRequired();

            builder.Property(reservation => reservation.Reference)
                .IsRequired();

            builder.Property(reservation => reservation.CreatedAt)
                .IsRequired();

            builder.Property(reservation => reservation.CreatedByUserId)
                .IsRequired();

            builder.Property(reservation => reservation.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(reservation => reservation.ReleasedAt);
            builder.Property(reservation => reservation.ReleasedByUserId);
            builder.Property(reservation => reservation.FulfilledAt);
            builder.Property(reservation => reservation.FulfilledByUserId);

            builder.Property<Guid>("RequestId")
                .IsRequired()
                .ValueGeneratedNever();

            builder.Property<Guid>("Version")
                .IsRequired()
                .IsConcurrencyToken()
                .ValueGeneratedNever();

            builder.HasOne<InventoryEntity>()
                .WithMany()
                .HasForeignKey(reservation => reservation.InventoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex("RequestId")
                .IsUnique()
                .HasDatabaseName("IX_StockReservations_RequestId");

            builder.HasIndex(reservation => new
            {
                reservation.InventoryId,
                reservation.Status
            });
        }
    }
}
