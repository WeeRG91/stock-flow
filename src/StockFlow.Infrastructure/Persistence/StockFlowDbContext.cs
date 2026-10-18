using Microsoft.EntityFrameworkCore;
using StockFlow.Domain.Entities;
using InventoryEntity = StockFlow.Domain.Entities.Inventory;

namespace StockFlow.Infrastructure.Persistence
{
    public sealed class StockFlowDbContext : DbContext
    {
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Warehouse> Warehouses => Set<Warehouse>();
        public DbSet<InventoryEntity> Inventories => Set<InventoryEntity>();
        public DbSet<StockMovement> StockMovements => Set<StockMovement>();
        public DbSet<LowStockAlert> LowStockAlerts => Set<LowStockAlert>();

        public StockFlowDbContext(
            DbContextOptions<StockFlowDbContext> options)
            : base(options)
        {}

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<InventoryEntity>()
                .Ignore(inventory => inventory.PendingMovements);

            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(StockFlowDbContext).Assembly);
        }
    }
}

