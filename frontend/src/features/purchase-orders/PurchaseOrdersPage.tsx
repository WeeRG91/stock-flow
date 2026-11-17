import { PageHeader } from "@/components/PageHeader";
import { usePurchaseOrders } from "./usePurchaseOrders";
import { FeedbackPanel } from "@/components/FeedbackPanel";
import { usePermissions } from "../auth/usePermissions";
import { CreatePurchaseOrderForm } from "./CreatePurchaseOrderForm";
import { useSuppliers } from "../suppliers/useSuppliers";
import { useWarehouses } from "../warehouses/useWarehouses";
import { Link } from "react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import type { PurchaseOrderStatus } from "@/types/api";
import { Label } from "@/components/ui/label";
import { statusLabels } from "./PurchaseOrderStatus";
import { Input } from "@/components/ui/input";

const orderDateFormat = new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
  });

export function PurchaseOrdersPage() {
  const [status, setStatus] = useState<PurchaseOrderStatus | undefined>();
  const [searchInput, setSearchInput] = useState<string>("");
  const [search, setSearch] = useState<string>("");
  const [pageNumber, setPageNumber] = useState<number>(1);
  const pageSize = 20;
  const query = usePurchaseOrders({
    pageNumber,
    pageSize,
    status,
    search: search || undefined,
  });

  const totalPages = query.data
    ? Math.max(1, Math.ceil(query.data.totalCount / query.data.pageSize))
    : 1;

  const { can } = usePermissions();

  return (
    <div className="space-y-6">
      <PageHeader
        title="Purchase Orders"
        description="Review NovaTrade’s purchase orders."
      />

      {can("PurchaseOrders.Manage") && <CreatePurchaseOrderSection />}

      <div className="grid gap-4 sm:grid-cols-[minmax(0,22rem)_12rem] sm:items-end">
        <form
          role="search"
          aria-label="Search purchase orders"
          className="min-w-0 space-y-2"
          onSubmit={(event) => {
            event.preventDefault();
            setSearch(searchInput.trim());
            setPageNumber(1);
          }}
        >
          <Label htmlFor="purchase-order-search">
            Order number, supplier code, or supplier name
          </Label>
          <div className="flex flex-col gap-2 sm:flex-row">
            <Input
              id="purchase-order-search"
              type="search"
              value={searchInput}
              onChange={(event) => setSearchInput(event.currentTarget.value)}
              className="h-11 min-w-0"
            />
            <Button type="submit" className="min-h-11">
              Search
            </Button>
          </div>
        </form>

        <div className="w-full space-y-2">
          <Label htmlFor="purchase-order-status">Status</Label>
          <select
            id="purchase-order-status"
            value={status ?? ""}
            className="h-11 w-full rounded-lg border border-input bg-card px-3 text-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
            onChange={(event) => {
              const value = event.currentTarget.value;
              setStatus(
                value === ""
                  ? undefined
                  : (Number(value) as PurchaseOrderStatus),
              );
              setPageNumber(1);
            }}
          >
            <option value="">All statuses</option>
            {Object.entries(statusLabels).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </div>
      </div>

      {query.isPending ? (
        <FeedbackPanel title="Loading purchase orders" role="status">
          <p>Fetching page {pageNumber}.</p>
        </FeedbackPanel>
      ) : query.isError ? (
        <FeedbackPanel title="Couldn't load purchase orders" role="alert">
          <p>Try refreshing the page.</p>
        </FeedbackPanel>
      ) : query.data.items.length === 0 ? (
        query.data.totalCount > 0 ? (
          <FeedbackPanel title="No orders on this page">
            <p>Go to a previous page to see the available orders.</p>
          </FeedbackPanel>
        ) : search || status !== undefined ? (
          <FeedbackPanel title="No orders match your filters">
            <p>Try another search or status.</p>
          </FeedbackPanel>
        ) : (
          <FeedbackPanel title="No purchase orders yet">
            <p>New purchase orders will appear here.</p>
          </FeedbackPanel>
        )
      ) : (
        <ul className="divide-y rounded-xl border bg-card">
          {query.data.items.map((order) => (
            <li
              key={order.id}
              className="flex flex-wrap items-start justify-between gap-3 px-6 py-4"
            >
              <div className="min-w-0 space-y-1">
                <Link
                  to={`/purchase-orders/${order.id}`}
                  className="wrap-anywhere rounded-sm font-medium text-primary underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-2"
                >
                  {order.orderNumber}
                </Link>
                <p className="text-sm text-muted-foreground">
                  {order.supplierName} · {order.warehouseName}
                </p>
                <p className="text-sm text-muted-foreground">
                  <time dateTime={order.createdAt}>
                    {orderDateFormat.format(new Date(order.createdAt))}
                  </time>
                  {" · "}
                  {order.lineCount} {order.lineCount === 1 ? "line" : "lines"}
                </p>
              </div>

              <span className="rounded-full bg-muted px-3 py-1 text-sm font-medium">
                {statusLabels[order.status]}
              </span>
            </li>
          ))}
        </ul>
      )}

      {(query.isSuccess || pageNumber > 1) && (
        <nav
          aria-label="Purchase order pagination"
          className="flex flex-wrap items-center justify-between gap-3"
        >
          <p className="text-sm text-muted-foreground" role="status">
            {query.data
              ? `Page ${query.data.pageNumber} of ${totalPages} · ${query.data.totalCount} total orders`
              : `Page ${pageNumber}`}
          </p>

          <div className="flex gap-2">
            <Button
              type="button"
              variant="outline"
              disabled={pageNumber <= 1 || query.isFetching}
              onClick={() => setPageNumber((current) => current - 1)}
            >
              Previous
            </Button>
            <Button
              type="button"
              variant="outline"
              disabled={
                !query.isSuccess || pageNumber >= totalPages || query.isFetching
              }
              onClick={() => setPageNumber((current) => current + 1)}
            >
              Next
            </Button>
          </div>
        </nav>
      )}
    </div>
  );
}

function CreatePurchaseOrderSection() {
  const suppliers = useSuppliers({
    pageNumber: 1,
    pageSize: 100,
    isActive: true,
  });
  const warehouses = useWarehouses();

  if (suppliers.isError || warehouses.isError) {
    return (
      <FeedbackPanel title="Couldn't load draft options" role="alert">
        <p>Refresh the page to try again.</p>
      </FeedbackPanel>
    );
  }

  if (suppliers.isPending || warehouses.isPending) {
    return (
      <FeedbackPanel title="Loading draft options" role="status">
        <p>Fetching suppliers and warehouses.</p>
      </FeedbackPanel>
    );
  }

  const activeWarehouses = warehouses.data.filter(
    (warehouse) => warehouse.isActive,
  );

  if (suppliers.data.items.length === 0 || activeWarehouses.length === 0) {
    return (
      <FeedbackPanel title="Draft creation is unavailable">
        <p>You need at least one active supplier and one active warehouse.</p>
      </FeedbackPanel>
    );
  }

  return (
    <CreatePurchaseOrderForm
      suppliers={suppliers.data.items}
      warehouses={activeWarehouses}
    />
  );
}
