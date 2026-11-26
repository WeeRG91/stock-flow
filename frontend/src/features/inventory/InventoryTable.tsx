import { Card, CardContent } from "@/components/ui/card";
import type { InventoryBalanceDto } from "@/types/api";

type InventoryTableProps = {
  items: readonly InventoryBalanceDto[];
};

const quantityColumns = [
  { key: "quantityOnHand", label: "On hand" },
  { key: "quantityReserved", label: "Reserved" },
  { key: "quantityAvailable", label: "Available" },
] as const;

export function InventoryTable({ items }: InventoryTableProps) {
  return (
    <Card className="gap-0 py-0">
      <div className="border-b px-6 py-5">
        <h2 className="text-base font-semibold">Stock balances</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Current quantities by product and warehouse.
        </p>
      </div>

      <CardContent className="px-0">
        <div
          role="region"
          aria-label="Stock balances table"
          tabIndex={0}
          className="overflow-x-auto focus-visible:outline-2 focus-visible:outline-offset-2"
        >
          <table className="w-full min-w-200 text-left text-sm">
            <caption className="sr-only">Inventory balances</caption>

            <thead className="border-b bg-muted/50 text-xs text-muted-foreground">
              <tr>
                <th scope="col" className="px-6 py-3 font-medium">
                  SKU
                </th>
                <th scope="col" className="px-6 py-3 font-medium">
                  Product
                </th>
                <th scope="col" className="px-6 py-3 font-medium">
                  Warehouse
                </th>
                {quantityColumns.map((column) => (
                  <th
                    key={column.key}
                    scope="col"
                    className="whitespace-nowrap px-6 py-3 text-right font-medium"
                  >
                    {column.label}
                  </th>
                ))}
              </tr>
            </thead>

            <tbody className="divide-y divide-border">
              {items.map((item) => (
                <tr key={item.inventoryId} className="hover:bg-muted/30">
                  <td className="whitespace-nowrap px-6 py-4 font-mono text-xs text-muted-foreground">
                    {item.sku}
                  </td>

                  <td className="px-6 py-4 font-medium">{item.productName}</td>

                  <td className="px-6 py-4">
                    <p>{item.warehouseName}</p>
                    <p className="mt-1 font-mono text-xs text-muted-foreground">
                      {item.warehouseCode}
                    </p>
                  </td>

                  {quantityColumns.map((column) => (
                    <td key={column.key} className="px-6 py-4 text-right">
                      <span className="inline-flex min-w-12 justify-center rounded-lg bg-muted px-3 py-1.5 font-semibold tabular-nums">
                        {item[column.key]}
                      </span>
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </CardContent>
    </Card>
  );
}
