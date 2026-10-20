using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockFlow.Domain.Entities;

namespace StockFlow.Infrastructure.Persistence.Configurations
{
    public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
    {
        public void Configure(EntityTypeBuilder<Supplier> builder)
        {
            builder.ToTable("Suppliers");

            builder.HasKey(supplier => supplier.Id);

            builder.Property(supplier => supplier.Id)
                .HasField("<Id>k__BackingField")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .ValueGeneratedNever();

            builder.Property(supplier => supplier.Code)
                .IsRequired()
                .HasMaxLength(Supplier.MaxCodeLength);

            builder.HasIndex(supplier => supplier.Code)
                .IsUnique()
                .HasDatabaseName("IX_Suppliers_Code");

            builder.Property(supplier => supplier.Name)
                .IsRequired()
                .HasMaxLength(Supplier.MaxNameLength);

            builder.Property(supplier => supplier.ContactEmail)
                .IsRequired()
                .HasMaxLength(Supplier.MaxContactEmailLength);

            builder.Property(supplier => supplier.PhoneNumber)
                .IsRequired(false)
                .HasMaxLength(Supplier.MaxPhoneNumberLength);

            builder.Property(supplier => supplier.IsActive)
                .IsRequired();
        }
    }
}
