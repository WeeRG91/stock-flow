import { useState } from "react";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/ApiError";
import { InventoryTable } from "./InventoryTable";
import { useInventory } from "./useInventory";
import { useWarehouses } from "../warehouses/useWarehouses";
import { useProducts } from "../products/useProducts";
import { ReceiveStockForm } from "./ReceiveStockForm";

const pageSize = 20;

export function InventoryPage() {
  const [pageNumber, setPageNumber] = useState(1);
  const query = useInventory(pageNumber, pageSize);

  const productsQuery = useProducts();
  const warehousesQuery = useWarehouses();

  const hasNextPage =
    query.data !== undefined &&
    pageNumber < Math.ceil(query.data.totalCount / pageSize);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Inventory"
        description="Review stock balances across NovaTrade's warehouses."
      />

      {productsQuery.isPending || warehousesQuery.isPending ? (
        <p role="status">Loading stock receipt options…</p>
      ) : productsQuery.isError || warehousesQuery.isError ? (
        <div role="alert" className="space-y-3">
          <p>Couldn't load products or warehouses for stock receipt.</p>
          <Button
            type="button"
            variant="outline"
            disabled={productsQuery.isFetching || warehousesQuery.isFetching}
            onClick={() => {
              void productsQuery.refetch();
              void warehousesQuery.refetch();
            }}
          >
            Retry loading options
          </Button>
        </div>
      ) : !productsQuery.data.some((product) => product.isActive) ||
        !warehousesQuery.data.some((warehouse) => warehouse.isActive) ? (
        <p>
          Stock receipt requires at least one active product and one active
          warehouse.
        </p>
      ) : (
        <ReceiveStockForm
          products={productsQuery.data}
          warehouses={warehousesQuery.data}
        />
      )}

      {query.isPending ? (
        <p role="status">Loading inventory…</p>
      ) : query.isError ? (
        <div role="alert" className="space-y-3">
          <p>
            {query.error instanceof ApiError
              ? query.error.message
              : "Couldn't load inventory. Check your connection and try again."}
          </p>
          <Button
            type="button"
            variant="outline"
            disabled={query.isFetching}
            onClick={() => void query.refetch()}
          >
            Try again
          </Button>
        </div>
      ) : query.data.items.length === 0 ? (
        <p>
          {query.data.totalCount === 0
            ? "No inventory balances exist yet. Receive stock to create one."
            : "No balances are available on this page. Go back to an earlier page."}
        </p>
      ) : (
        <InventoryTable items={query.data.items} />
      )}

      <nav
        aria-label="Inventory pagination"
        className="flex flex-wrap items-center justify-between gap-3"
      >
        <p className="text-sm text-muted-foreground" aria-live="polite">
          Page {pageNumber}
          {query.isSuccess ? ` · ${query.data.totalCount} balances` : ""}
        </p>

        <div className="flex gap-2">
          <Button
            type="button"
            variant="outline"
            disabled={pageNumber === 1 || query.isFetching}
            onClick={() => setPageNumber((page) => Math.max(1, page - 1))}
          >
            Previous
          </Button>

          <Button
            type="button"
            variant="outline"
            disabled={!query.isSuccess || !hasNextPage || query.isFetching}
            onClick={() => setPageNumber((page) => page + 1)}
          >
            Next
          </Button>
        </div>
      </nav>
    </div>
  );
}
