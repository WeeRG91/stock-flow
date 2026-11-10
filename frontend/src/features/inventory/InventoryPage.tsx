import { useState } from "react";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/ApiError";
import { InventoryTable } from "./InventoryTable";
import { useInventory } from "./useInventory";
import { useWarehouses } from "../warehouses/useWarehouses";
import { useProducts } from "../products/useProducts";
import { ReceiveStockForm } from "./ReceiveStockForm";
import { useInventoryRealtime } from "./useInventoryRealtime";
import { FeedbackPanel } from "@/components/FeedbackPanel";
import { usePermissions } from "../auth/usePermissions";

const pageSize = 20;

export function InventoryPage() {
  const [pageNumber, setPageNumber] = useState(1);
  const query = useInventory(pageNumber, pageSize);

  const productsQuery = useProducts();
  const warehousesQuery = useWarehouses();
  const connectionStatus = useInventoryRealtime();
  const { can } = usePermissions();
  const canReceiveStock = can("Inventory.Receive");

  const hasNextPage =
    query.data !== undefined &&
    pageNumber < Math.ceil(query.data.totalCount / pageSize);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Inventory"
        description="Review stock balances across NovaTrade's warehouses."
        actions={
          <span
            role="status"
            className={[
              "inline-flex min-h-9 items-center gap-2 rounded-full border px-3 py-1.5 text-xs font-medium",
              connectionStatus === "connected"
                ? "border-primary/20 bg-accent text-accent-foreground"
                : "border-border bg-card text-muted-foreground",
            ].join(" ")}
          >
            <span
              aria-hidden="true"
              className="size-2 shrink-0 rounded-full bg-current"
            />

            {connectionStatus === "connected"
              ? "Live updates connected"
              : connectionStatus === "connecting"
                ? "Connecting to live updates…"
                : connectionStatus === "reconnecting"
                  ? "Reconnecting to live updates…"
                  : "Disconnected · retrying automatically…"}
          </span>
        }
      />

      {canReceiveStock && (
        <>
          {productsQuery.isPending || warehousesQuery.isPending ? (
            <FeedbackPanel title="Preparing stock receipt" role="status">
              <p>Loading products and warehouse options…</p>
            </FeedbackPanel>
          ) : productsQuery.isError || warehousesQuery.isError ? (
            <FeedbackPanel title="Stock receipt unavailable" role="alert">
              <p>Couldn't load products or warehouses for stock receipt.</p>

              <Button
                type="button"
                variant="outline"
                className="min-h-11"
                disabled={
                  productsQuery.isFetching || warehousesQuery.isFetching
                }
                onClick={() => {
                  void productsQuery.refetch();
                  void warehousesQuery.refetch();
                }}
              >
                Retry loading options
              </Button>
            </FeedbackPanel>
          ) : !productsQuery.data.some((product) => product.isActive) ||
            !warehousesQuery.data.some((warehouse) => warehouse.isActive) ? (
            <FeedbackPanel title="Stock receipt needs active catalog entries">
              <p>
                Stock receipt requires at least one active product and one
                active warehouse.
              </p>
            </FeedbackPanel>
          ) : (
            <ReceiveStockForm
              products={productsQuery.data}
              warehouses={warehousesQuery.data}
            />
          )}
        </>
      )}

      {query.isPending ? (
        <FeedbackPanel title="Loading inventory" role="status">
          <p>Fetching current stock balances…</p>
        </FeedbackPanel>
      ) : query.isError ? (
        <FeedbackPanel title="Couldn't load inventory" role="alert">
          <p>
            {query.error instanceof ApiError
              ? query.error.message
              : "Couldn't load inventory. Check your connection and try again."}
          </p>

          <Button
            type="button"
            variant="outline"
            className="min-h-11"
            disabled={query.isFetching}
            onClick={() => void query.refetch()}
          >
            Try again
          </Button>
        </FeedbackPanel>
      ) : query.data.items.length === 0 ? (
        <FeedbackPanel
          title={
            query.data.totalCount === 0
              ? "No inventory balances yet"
              : "No balances on this page"
          }
        >
          <p>
            {query.data.totalCount === 0
              ? canReceiveStock
                ? "Receive stock to create the first inventory balance."
                : "No inventory balances are available yet."
              : "Go back to an earlier page to view available balances."}
          </p>
        </FeedbackPanel>
      ) : (
        <InventoryTable items={query.data.items} />
      )}

      <nav
        aria-label="Inventory pagination"
        className="flex flex-wrap items-center justify-between gap-4 rounded-xl border border-border bg-card px-5 py-4 shadow-sm"
      >
        <p className="text-sm text-muted-foreground" aria-live="polite">
          Page {pageNumber}
          {query.isSuccess ? ` · ${query.data.totalCount} balances` : ""}
        </p>

        <div className="flex gap-2">
          <Button
            type="button"
            variant="outline"
            className="min-h-11"
            disabled={pageNumber === 1 || query.isFetching}
            onClick={() => setPageNumber((page) => Math.max(1, page - 1))}
          >
            Previous
          </Button>

          <Button
            type="button"
            variant="outline"
            className="min-h-11"
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
