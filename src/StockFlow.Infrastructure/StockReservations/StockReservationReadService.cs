using Microsoft.EntityFrameworkCore;
using StockFlow.Application.StockReservations;
using StockFlow.Application.StockReservations.GetStockReservations;
using StockFlow.Domain.Enums;
using StockFlow.Infrastructure.Persistence;

namespace StockFlow.Infrastructure.StockReservations
{
    public sealed class StockReservationReadService : IStockReservationReadService
    {
        private readonly StockFlowDbContext _context;

        public StockReservationReadService(StockFlowDbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        public Task<StockReservationDto?> GetByIdAsync(
            Guid reservationId,
            CancellationToken cancellationToken = default)
        {
            var query =
                from reservation in _context.StockReservations.AsNoTracking()
                join inventory in _context.Inventories
                    on reservation.InventoryId equals inventory.Id
                join product in _context.Products
                    on inventory.ProductId equals product.Id
                join warehouse in _context.Warehouses
                    on inventory.WarehouseId equals warehouse.Id
                where reservation.Id == reservationId
                select new StockReservationDto(
                    reservation.Id,
                    inventory.Id,
                    product.Id,
                    product.Name,
                    product.Sku,
                    warehouse.Id,
                    warehouse.Name,
                    warehouse.Code,
                    reservation.Quantity,
                    reservation.Reference,
                    reservation.Status,
                    reservation.CreatedAt,
                    reservation.CreatedByUserId,
                    reservation.ReleasedAt,
                    reservation.ReleasedByUserId,
                    reservation.FulfilledAt,
                    reservation.FulfilledByUserId,
                    EF.Property<Guid>(reservation, "Version"));

            return query.SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<GetStockReservationsResult> GetPageAsync(
            int pageNumber,
            int pageSize,
            StockReservationStatus? status,
            string? search,
            Guid? inventoryId,
            CancellationToken cancellationToken = default)
        {
            var query =
                from reservation in _context.StockReservations.AsNoTracking()
                join inventory in _context.Inventories
                    on reservation.InventoryId equals inventory.Id
                join product in _context.Products
                    on inventory.ProductId equals product.Id
                join warehouse in _context.Warehouses
                    on inventory.WarehouseId equals warehouse.Id
                select new
                {
                    Reservation = reservation,
                    Inventory = inventory,
                    Product = product,
                    Warehouse = warehouse
                };

            if (status is { } selectedStatus)
            {
                query = query.Where(
                    item => item.Reservation.Status == selectedStatus);
            }

            if (inventoryId is { } selectedInventoryId)
            {
                query = query.Where(
                    item => item.Inventory.Id == selectedInventoryId);
            }

            if (search is not null)
            {
                var escapedSearch = search
                    .Replace("\\", "\\\\")
                    .Replace("%", "\\%")
                    .Replace("_", "\\_");

                var pattern = $"%{escapedSearch}%";

                query = query.Where(item =>
                    EF.Functions.ILike(item.Reservation.Reference, pattern, "\\") ||
                    EF.Functions.ILike(item.Product.Name, pattern, "\\") ||
                    EF.Functions.ILike(item.Product.Sku, pattern, "\\") ||
                    EF.Functions.ILike(item.Warehouse.Name, pattern, "\\") ||
                    EF.Functions.ILike(item.Warehouse.Code, pattern, "\\"));
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var offset = checked((int)(((long)pageNumber - 1) * pageSize));

            var items = await query
                .OrderByDescending(item => item.Reservation.CreatedAt)
                .ThenByDescending(item => item.Reservation.Id)
                .Skip(offset)
                .Take(pageSize)
                .Select(item => new StockReservationDto(
                    item.Reservation.Id,
                    item.Inventory.Id,
                    item.Product.Id,
                    item.Product.Name,
                    item.Product.Sku,
                    item.Warehouse.Id,
                    item.Warehouse.Name,
                    item.Warehouse.Code,
                    item.Reservation.Quantity,
                    item.Reservation.Reference,
                    item.Reservation.Status,
                    item.Reservation.CreatedAt,
                    item.Reservation.CreatedByUserId,
                    item.Reservation.ReleasedAt,
                    item.Reservation.ReleasedByUserId,
                    item.Reservation.FulfilledAt,
                    item.Reservation.FulfilledByUserId,
                    EF.Property<Guid>(item.Reservation, "Version")))
                .ToListAsync(cancellationToken);

            return new GetStockReservationsResult(
                items,
                pageNumber,
                pageSize,
                totalCount);
        }
    }
}
