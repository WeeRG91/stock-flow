import { PageHeader } from "@/components/PageHeader";
import { useTransfers } from "./useTransfers";
import { FeedbackPanel } from "@/components/FeedbackPanel";
import { transferStatusLabels } from "./transferStatus";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import type { TransferStatus } from "@/types/api";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Link } from "react-router";
import { usePermissions } from "../auth/usePermissions";
import { CreateTransferSection } from "./CreateTransferSection";

const transferDateFormat = new Intl.DateTimeFormat(undefined, {
  dateStyle: "medium",
  timeStyle: "short",
});

export function TransfersPage() {
  const [pageNumber, setPageNumber] = useState<number>(1);
  const [status, setStatus] = useState<TransferStatus | undefined>();
  const [searchInput, setSearchInput] = useState<string>("");
  const [search, setSearch] = useState<string>("");

  const { can } = usePermissions();

  const pageSize = 20;

  const query = useTransfers({
    pageNumber,
    pageSize,
    status,
    search: search || undefined,
  });

  const totalPages = query.data
    ? Math.max(1, Math.ceil(query.data.totalCount / query.data.pageSize))
    : 1;

  const hasFilters = status !== undefined || search.length > 0;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Warehouse transfers"
        description="Review stock transfers between NovaTrade’s warehouses."
      />

      {can("Transfers.Manage") && <CreateTransferSection />}

      <div className="grid gap-4 sm:grid-cols-[12rem_minmax(0,22rem)] sm:items-end">
        <div className="w-full space-y-2 sm:max-w-48">
          <Label htmlFor="transfer-status">Status</Label>

          <select
            id="transfer-status"
            value={status ?? ""}
            className="h-11 w-full rounded-lg border border-input bg-card px-3 text-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
            onChange={(event) => {
              const value = event.currentTarget.value;

              setStatus(
                value === "" ? undefined : (Number(value) as TransferStatus),
              );
              setPageNumber(1);
            }}
          >
            <option value="">All statuses</option>

            {Object.entries(transferStatusLabels).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </div>

        <form
          role="search"
          aria-label="Search warehouse transfers"
          className="min-w-0 space-y-2"
          onSubmit={(event) => {
            event.preventDefault();
            setSearch(searchInput.trim());
            setPageNumber(1);
          }}
        >
          <Label htmlFor="transfer-search">Warehouse code or name</Label>

          <div className="flex flex-col gap-2 sm:flex-row">
            <Input
              id="transfer-search"
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
        <FeedbackPanel title="Loading transfers" role="status">
          <p>Fetching warehouse transfers.</p>
        </FeedbackPanel>
      ) : query.isError ? (
        <FeedbackPanel title="Couldn't load transfers" role="alert">
          <p>{query.error.message}</p>
        </FeedbackPanel>
      ) : query.data.items.length === 0 ? (
        <FeedbackPanel
          title={
            query.data.totalCount > 0
              ? "No transfers on this page"
              : hasFilters
                ? "No transfers match your filters"
                : "No transfers yet"
          }
        >
          <p>
            {query.data.totalCount > 0
              ? "Go to a previous page to see available transfers."
              : hasFilters
                ? "Try another warehouse search or status."
                : "New warehouse transfers will appear here."}
          </p>
        </FeedbackPanel>
      ) : (
        <ul
          aria-label="Warehouse transfers"
          className="divide-y rounded-xl border bg-card"
        >
          {query.data.items.map((transfer) => (
            <li key={transfer.id} className="space-y-2 px-6 py-4">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <Link
                  to={`/transfers/${transfer.id}`}
                  className="min-w-0 wrap-anywhere rounded-sm font-medium text-primary underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-4"
                >
                  {transfer.sourceWarehouseName}
                  {" → "}
                  {transfer.destinationWarehouseName}
                </Link>

                <span className="shrink-0 rounded-full bg-muted px-3 py-1 text-xs font-medium">
                  {transferStatusLabels[transfer.status]}
                </span>
              </div>

              <p className="text-sm text-muted-foreground">
                {transfer.sourceWarehouseCode}
                {" → "}
                {transfer.destinationWarehouseCode}
                {" · "}
                {transfer.lineCount} product lines
              </p>
              <p className="text-sm text-muted-foreground">
                Created{" "}
                <time dateTime={transfer.createdAt}>
                  {transferDateFormat.format(new Date(transfer.createdAt))}
                </time>
              </p>
              {transfer.dispatchedAt !== null && (
                <p className="text-sm text-muted-foreground">
                  Dispatched{" "}
                  <time dateTime={transfer.dispatchedAt}>
                    {transferDateFormat.format(new Date(transfer.dispatchedAt))}
                  </time>
                </p>
              )}
            </li>
          ))}
        </ul>
      )}

      <nav
        aria-label="Transfer pagination"
        className="flex flex-wrap items-center justify-between gap-3"
      >
        <p className="text-sm text-muted-foreground" role="status">
          {query.data
            ? `Page ${query.data.pageNumber} of ${totalPages} · ${query.data.totalCount} total transfers`
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
    </div>
  );
}
