using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace StockFlow.Infrastructure.Identity
{
    public sealed class StockFlowIdentityDbContext
        : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {
        public StockFlowIdentityDbContext(
            DbContextOptions<StockFlowIdentityDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema("identity");

            modelBuilder.Entity<ApplicationUser>(user =>
            {
                user.Property(value => value.DisplayName)
                    .IsRequired()
                    .HasMaxLength(200);

                user.Property(value => value.Email)
                    .IsRequired()
                    .HasMaxLength(256);

                user.Property(value => value.NormalizedEmail)
                    .IsRequired()
                    .HasMaxLength(256);

                user.HasIndex(value => value.NormalizedEmail)
                    .HasDatabaseName("EmailIndex")
                    .IsUnique();
            });
        }
    }
}
