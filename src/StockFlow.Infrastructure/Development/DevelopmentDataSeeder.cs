using Microsoft.EntityFrameworkCore;
using StockFlow.Domain.Entities;
using StockFlow.Infrastructure.Persistence;

namespace StockFlow.Infrastructure.Development
{
    public static class DevelopmentDataSeeder
    {
        public static async Task SeedAsync(
            StockFlowDbContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var products = new[]
            {
                new Product("[DEMO] Ergonomic Office Chair", "DEMO-CHAIR-ERG",
                    "Synthetic product - adjustable lumbar support."),
                new Product("[DEMO] Visitor Chair", "DEMO-CHAIR-VIS",
                    "Synthetic product - stackable meeting room chair."),
                new Product("[DEMO] Standing Desk", "DEMO-DESK-STAND",
                    "Synthetic product - height-adjustable workstation."),
                new Product("[DEMO] Office Desk 140 cm", "DEMO-DESK-140",
                    "Synthetic product - standard office workstation."),
                new Product("[DEMO] Filing Cabinet", "DEMO-CABINET",
                    "Synthetic product - lockable document storage."),
                new Product("[DEMO] LED Desk Lamp", "DEMO-LAMP-LED",
                    "Synthetic product - adjustable task lighting."),
                new Product("[DEMO] Monitor 27 inch", "DEMO-MONITOR-27",
                    "Synthetic product - office display."),
                new Product("[DEMO] Monitor Arm", "DEMO-MONITOR-ARM",
                    "Synthetic product - single-display desk mount."),
                new Product("[DEMO] USB-C Dock", "DEMO-DOCK-USBC",
                    "Synthetic product - laptop docking station."),
                new Product("[DEMO] Wireless Keyboard", "DEMO-KEYBOARD",
                    "Synthetic product - office keyboard."),
                new Product("[DEMO] Wireless Mouse", "DEMO-MOUSE",
                    "Synthetic product - office pointing device."),
                new Product("[DEMO] USB Headset", "DEMO-HEADSET",
                    "Synthetic product - headset for calls."),
                new Product("[DEMO] Webcam", "DEMO-WEBCAM",
                    "Synthetic product - video conferencing camera."),
                new Product("[DEMO] Laptop Stand", "DEMO-LAPTOP-STAND",
                    "Synthetic product - adjustable laptop support."),
                new Product("[DEMO] Ethernet Cable 2 m", "DEMO-CABLE-ETH2",
                    "Synthetic product - network cable."),
                new Product("[DEMO] HDMI Cable 2 m", "DEMO-CABLE-HDMI2",
                    "Synthetic product - display cable."),
                new Product("[DEMO] Power Strip", "DEMO-POWER-STRIP",
                    "Synthetic product - six-outlet power strip."),
                new Product("[DEMO] A4 Paper Box", "DEMO-PAPER-A4",
                    "Synthetic product - one stock unit is a box."),
                new Product("[DEMO] Black Toner Cartridge", "DEMO-TONER-BLK",
                    "Synthetic product - replacement printer toner."),
                new Product("[DEMO] Notebook Pack", "DEMO-NOTEBOOK-PACK",
                    "Synthetic product - one stock unit is a pack."),
                new Product("[DEMO] Ballpoint Pen Pack", "DEMO-PEN-PACK",
                    "Synthetic product - one stock unit is a pack."),
                new Product("[DEMO] Whiteboard", "DEMO-WHITEBOARD",
                    "Synthetic product - meeting room board."),
                new Product("[DEMO] Storage Box", "DEMO-STORAGE-BOX",
                    "Synthetic product - reusable storage container."),
                new Product("[DEMO] Shipping Carton", "DEMO-CARTON",
                    "Synthetic product - packaging carton."),
                new Product("[DEMO] Packing Tape Roll", "DEMO-TAPE",
                    "Synthetic product - packaging tape.")
            };

            foreach (var product in products)
            {
                var exists = await context.Products.AnyAsync(
                    existing => existing.Sku == product.Sku,
                    cancellationToken);

                if (!exists)
                {
                    context.Products.Add(product);
                }
            }

            var warehouses = new[]
            {
                new Warehouse(
                    "DEMO-BRU",
                    "[DEMO] Brussels Warehouse",
                    "Brussels, Belgium"),
                new Warehouse(
                    "DEMO-LIE",
                    "[DEMO] Liege Warehouse",
                    "Liege, Belgium"),
                new Warehouse(
                    "DEMO-LUX",
                    "[DEMO] Luxembourg Warehouse",
                    "Luxembourg, Luxembourg")
            };

            foreach (var warehouse in warehouses)
            {
                var exists = await context.Warehouses.AnyAsync(
                    existing => existing.Code == warehouse.Code,
                    cancellationToken);

                if (!exists)
                {
                    context.Warehouses.Add(warehouse);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}