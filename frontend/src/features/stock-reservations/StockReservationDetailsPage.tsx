import type { StockReservationStatus } from "@/types/api";
import { Link, useParams } from "react-router";
import { useStockReservation } from "./useStockReservation";
import { ApiError } from "@/lib/ApiError";
import { PageHeader } from "@/components/PageHeader";
import { FeedbackPanel } from "@/components/FeedbackPanel";
import { Button } from "@/components/ui/button";
import { StockReservationHistory } from "./StockReservationHistory";
import { usePermissions } from "../auth/usePermissions";
import { StockReservationActions } from "./StockReservationActions";
import { useInventoryRealtime } from "../inventory/useInventoryRealtime";

const statusLabels: Record<StockReservationStatus, string> = {
  1: "Active",
  2: "Released",
  3: "Fulfilled",
};

export function StockReservationDetailsPage() {
  const { id } = useParams<{ id: string }>();
  const query = useStockReservation(id ?? "");
  const { can } = usePermissions();

  const isNotFound =
    query.error instanceof ApiError && query.error.status === 404;

  useInventoryRealtime();

  return (
    <div className="space-y-6">
      <PageHeader
        title="Reservation details"
        description="Review the stock allocation and its lifecycle."
      />

      <Link
        to="/stock-reservations"
        className="inline-flex min-h-11 items-center rounded-sm text-primary underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-4"
      >
        Back to reservations
      </Link>

      {!id ? (
        <FeedbackPanel title="Missing reservation identifier" role="alert">
          <p>A reservation identifier is required.</p>
        </FeedbackPanel>
      ) : query.isPending ? (
        <FeedbackPanel title="Loading reservation" role="status">
          <p>Fetching reservation details…</p>
        </FeedbackPanel>
      ) : query.isError ? (
        <FeedbackPanel
          title={
            isNotFound ? "Reservation not found" : "Couldn't load reservation"
          }
          role="alert"
        >
          <p>
            {isNotFound
              ? "Return to the reservation list to select another."
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
        <section
          aria-labelledby="reservation-heading"
          className="space-y-6 rounded-xl border bg-card p-6 shadow-sm"
        >
          <div className="flex flex-wrap items-start justify-between gap-3">
            <h2
              id="reservation-heading"
              className="min-w-0 text-lg font-semibold wrap-break-word"
            >
              {query.data.reference}
            </h2>
            <span className="rounded-full bg-muted px-3 py-1 text-xs font-medium">
              {statusLabels[query.data.status]}
            </span>
          </div>

          <dl className="grid gap-6 sm:grid-cols-2">
            <div>
              <dt className="text-xs text-muted-foreground">Product</dt>
              <dd className="mt-1 text-sm wrap-break-word">
                <Link
                  to={`/products/${query.data.productId}`}
                  className="text-primary underline underline-offset-4"
                >
                  {query.data.productName}
                </Link>
                <p className="mt-1 font-mono text-xs text-muted-foreground">
                  {query.data.sku}
                </p>
              </dd>
            </div>

            <div>
              <dt className="text-xs text-muted-foreground">Warehouse</dt>
              <dd className="mt-1 text-sm wrap-break-word">
                <Link
                  to={`/warehouses/${query.data.warehouseId}`}
                  className="text-primary underline underline-offset-4"
                >
                  {query.data.warehouseName}
                </Link>
                <p className="mt-1 font-mono text-xs text-muted-foreground">
                  {query.data.warehouseCode}
                </p>
              </dd>
            </div>

            <div>
              <dt className="text-xs text-muted-foreground">
                Reservation quantity
              </dt>
              <dd className="mt-1 font-semibold tabular-nums">
                {query.data.quantity}
              </dd>
            </div>
          </dl>

          {(can("StockReservations.Manage") ||
            can("StockReservations.Fulfill")) && (
            <StockReservationActions
              key={query.data.reservationId}
              reservation={query.data}
              refreshing={query.isFetching}
              canRelease={can("StockReservations.Manage")}
              canFulfill={can("StockReservations.Fulfill")}
              onRefresh={() => void query.refetch()}
            />
          )}

          <StockReservationHistory reservation={query.data} />
        </section>
      )}
    </div>
  );
}
