import { Card, CardContent } from "@/components/ui/card";
import type { InventoryBalanceDto } from "@/types/api";

type InventoryTableProps = {
  items: readonly InventoryBalanceDto[];
};

export function InventoryTable({ items }: InventoryTableProps) {
  return (
    <Card>
      <CardContent>
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <caption className="sr-only">Inventory balances</caption>
            <thead>
              <tr className="border-b">
                <th scope="col" className="px-3 py-3">
                  SKU
                </th>
                <th scope="col" className="px-3 py-3">
                  Product
                </th>
                <th scope="col" className="px-3 py-3">
                  Warehouse
                </th>
                <th scope="col" className="px-3 py-3 text-right">
                  Quantity on hand
                </th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr key={item.inventoryId} className="border-b last:border-0">
                  <td className="whitespace-nowrap px-3 py-3 font-medium">
                    {item.sku}
                  </td>
                  <td className="px-3 py-3">{item.productName}</td>
                  <td className="px-3 py-3">
                    <p>{item.warehouseName}</p>
                    <p className="text-xs text-muted-foreground">
                      {item.warehouseCode}
                    </p>
                  </td>
                  <td className="px-3 py-3 text-right tabular-nums">
                    {item.quantityOnHand}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </CardContent>
    </Card>
  );
}
