using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockFlow.Domain.Entities;

namespace StockFlow.Infrastructure.Persistence.Configurations
{
    public sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
    {
        public void Configure(EntityTypeBuilder<Warehouse> builder)
        {
            builder.ToTable("Warehouses");

            builder.HasKey(warehouse => warehouse.Id);

            builder.Property(warehouse => warehouse.Id)
                .HasField("<Id>k__BackingField")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .ValueGeneratedNever();

            builder.Property(warehouse => warehouse.Code)
                .IsRequired()
                .HasMaxLength(32);

            builder.HasIndex(warehouse => warehouse.Code)
                .IsUnique();

            builder.Property(warehouse => warehouse.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(warehouse => warehouse.Location)
                .IsRequired()
                .HasMaxLength(300);

            builder.Property(warehouse => warehouse.IsActive)
                .IsRequired();
        }
    }
}
