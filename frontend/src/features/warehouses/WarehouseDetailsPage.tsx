import { Link, useParams } from "react-router";
import { PageHeader } from "@/components/PageHeader";
import { FeedbackPanel } from "@/components/FeedbackPanel";
import { StatusBadge } from "@/components/StatusBadge";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/ApiError";
import { usePermissions } from "@/features/auth/usePermissions";
import { FilteredInventoryBalances } from "@/features/inventory/FilteredInventoryBalance";
import { useWarehouse } from "./useWarehouse";

export function WarehouseDetailsPage() {
  const { id } = useParams<{ id: string }>();
  const query = useWarehouse(id ?? "");
  const { can } = usePermissions();

  const isNotFound =
    query.error instanceof ApiError && query.error.status === 404;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Warehouse details"
        description="View warehouse information and product stock balances."
      />

      <Link
        to="/warehouses"
        className="inline-flex min-h-11 items-center rounded-lg text-sm font-medium text-primary underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-4"
      >
        Back to warehouses
      </Link>

      {!id ? (
        <FeedbackPanel title="Missing warehouse identifier" role="alert">
          <p>A warehouse identifier is required.</p>
        </FeedbackPanel>
      ) : query.isPending ? (
        <FeedbackPanel title="Loading warehouse" role="status">
          <p>Fetching warehouse information…</p>
        </FeedbackPanel>
      ) : query.isError ? (
        <FeedbackPanel
          title={isNotFound ? "Warehouse not found" : "Couldn't load warehouse"}
          role="alert"
        >
          <p>
            {isNotFound
              ? "Return to the warehouse list to select another warehouse."
              : query.error.message}
          </p>

          {!isNotFound && (
            <Button
              type="button"
              variant="outline"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              Try again
            </Button>
          )}
        </FeedbackPanel>
      ) : (
        <>
          <section
            aria-labelledby="warehouse-heading"
            className="space-y-4 rounded-xl border bg-card p-6 shadow-sm"
          >
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div className="min-w-0">
                <h2
                  id="warehouse-heading"
                  className="text-lg font-semibold wrap-break-word"
                >
                  {query.data.name}
                </h2>
                <p className="mt-1 font-mono text-sm text-muted-foreground wrap-break-word">
                  {query.data.code}
                </p>
              </div>

              <StatusBadge isActive={query.data.isActive} />
            </div>

            <dl>
              <dt className="text-xs font-medium text-muted-foreground">
                Location
              </dt>
              <dd className="mt-1 text-sm wrap-break-word">
                {query.data.location}
              </dd>
            </dl>
          </section>

          {can("Inventory.Read") && (
            <FilteredInventoryBalances
              key={query.data.id}
              filters={{ warehouseId: query.data.id }}
            />
          )}
        </>
      )}
    </div>
  );
}