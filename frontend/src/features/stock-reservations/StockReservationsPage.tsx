import type { StockReservationStatus } from "@/types/api";
import { useStockReservations } from "./useStockReservations";
import { PageHeader } from "@/components/PageHeader";
import { FeedbackPanel } from "@/components/FeedbackPanel";
import { Button } from "@/components/ui/button";
import { useState } from "react";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Link } from "react-router";
import { usePermissions } from "../auth/usePermissions";
import { CreateStockReservationSection } from "./CreateStockReservationSection";
import { useInventoryRealtime } from "../inventory/useInventoryRealtime";

const statusLabels: Record<StockReservationStatus, string> = {
  1: "Active",
  2: "Released",
  3: "Fulfilled",
};

export function StockReservationsPage() {
  const [pageNumber, setPageNumber] = useState<number>(1);
  const [status, setStatus] = useState<StockReservationStatus>();
  const [searchInput, setSearchInput] = useState<string>("");
  const [search, setSearch] = useState<string>("");

  const query = useStockReservations({
    pageNumber,
    pageSize: 20,
    status,
    search: search || undefined,
  });

  const {can} = usePermissions();

  const hasFilters = status !== undefined || search.length > 0;

  const totalPages = Math.max(1, Math.ceil((query.data?.totalCount ?? 0) / 20));

  useInventoryRealtime();

  return (
    <div className="space-y-6">
      <PageHeader
        title="Stock reservations"
        description="Review allocated stock and reservation history."
      />

      {can("StockReservations.Manage") && (
        <CreateStockReservationSection />
      )}

      <div className="grid gap-4 sm:grid-cols-[12rem_minmax(0,22rem)] sm:items-end">
        <div className="space-y-2">
          <Label htmlFor="reservation-status">Status</Label>
          <select
            id="reservation-status"
            value={status ?? ""}
            className="h-11 w-full rounded-lg border border-input bg-card px-3 text-sm focus-visible:outline-2 focus-visible:outline-offset-2"
            onChange={(event) => {
              const value = event.currentTarget.value;
              setStatus(
                value === ""
                  ? undefined
                  : (Number(value) as StockReservationStatus),
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

        <form
          role="search"
          aria-label="Search stock reservations"
          className="min-w-0 space-y-2"
          onSubmit={(event) => {
            event.preventDefault();
            setSearch(searchInput.trim());
            setPageNumber(1);
          }}
        >
          <Label htmlFor="reservation-search">
            Reference, product, or warehouse
          </Label>
          <div className="flex flex-col gap-2 sm:flex-row">
            <Input
              id="reservation-search"
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
      </div>

      {query.isPending ? (
        <FeedbackPanel title="Loading reservations" role="status">
          <p>Fetching stock reservations…</p>
        </FeedbackPanel>
      ) : query.isError ? (
        <FeedbackPanel title="Couldn't load reservations" role="alert">
          <p>{query.error.message}</p>
          <Button
            type="button"
            variant="outline"
            disabled={query.isFetching}
            onClick={() => void query.refetch()}
          >
            Try again
          </Button>
        </FeedbackPanel>
      ) : query.data.items.length === 0 ? (
        <FeedbackPanel
          title={
            query.data.totalCount > 0
              ? "No reservations on this page"
              : hasFilters
                ? "No matching reservations"
                : "No reservations yet"
          }
        >
          <p>
            {query.data.totalCount > 0
              ? "Return to a previous page to view reservations."
              : hasFilters
                ? "Try another status or search term."
                : "No stock reservations have been recorded."}
          </p>
        </FeedbackPanel>
      ) : (
        <section aria-label="Reservation results" className="space-y-4">
          <p role="status" className="text-sm text-muted-foreground">
            Showing {query.data.items.length} of {query.data.totalCount}{" "}
            reservations.
          </p>

          <ul className="divide-y rounded-xl border bg-card shadow-sm">
            {query.data.items.map((reservation) => (
              <li key={reservation.reservationId} className="space-y-3 p-6">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <h2 className="min-w-0 font-semibold wrap-break-word">
                    <Link
                      to={`/stock-reservations/${reservation.reservationId}`}
                      className="rounded-sm underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-4"
                    >
                      {reservation.reference}
                    </Link>
                  </h2>
                  <span className="rounded-full bg-muted px-3 py-1 text-xs font-medium">
                    {statusLabels[reservation.status]}
                  </span>
                </div>

                <p className="text-sm wrap-break-word">
                  {reservation.productName} · {reservation.sku}
                </p>

                <div className="flex flex-wrap justify-between gap-3 text-sm text-muted-foreground">
                  <p>
                    {reservation.warehouseName} · {reservation.warehouseCode}
                  </p>
                  <p className="font-medium tabular-nums">
                    Reservation quantity: {reservation.quantity}
                  </p>
                </div>
              </li>
            ))}
          </ul>
        </section>
      )}

      <nav
        aria-label="Reservation pagination"
        className="flex flex-wrap items-center justify-between gap-3"
      >
        <Button
          type="button"
          variant="outline"
          className="min-h-11"
          disabled={pageNumber <= 1 || query.isFetching}
          onClick={() => setPageNumber((page) => page - 1)}
        >
          Previous
        </Button>

        <p role="status" className="text-sm text-muted-foreground">
          Page {pageNumber}
          {query.isSuccess && ` of ${totalPages}`}
        </p>

        <Button
          type="button"
          variant="outline"
          className="min-h-11"
          disabled={
            !query.isSuccess || pageNumber >= totalPages || query.isFetching
          }
          onClick={() => setPageNumber((page) => page + 1)}
        >
          Next
        </Button>
      </nav>
    </div>
  );
}
