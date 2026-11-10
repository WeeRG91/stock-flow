import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/ApiError";
import { useSuppliers } from "./useSuppliers";
import { SuppliersTable } from "./SuppliersTable";
import { useState } from "react";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { CreateSupplierForm } from "./CreateSupplierForm";
import { FeedbackPanel } from "@/components/FeedbackPanel";
import { usePermissions } from "../auth/usePermissions";

export function SuppliersPage() {
  const [searchInput, setSearchInput] = useState<string>("");
  const [search, setSearch] = useState<string>("");
  const [status, setStatus] = useState("all");
  const [pageNumber, setPageNumber] = useState<number>(1);
  const [pageSize, setPageSize] = useState<number>(20);

  const query = useSuppliers({
    pageNumber,
    pageSize,
    search,
    isActive: status === "all" ? undefined : status === "active",
  });

  const { can } = usePermissions();
  const canManageSuppliers = can("Suppliers.Manage");

  const totalPages = query.data
    ? Math.max(1, Math.ceil(query.data.totalCount / pageSize))
    : 1;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Suppliers"
        description="Manage NovaTrade's suppliers and contact information."
      />

      {canManageSuppliers && <CreateSupplierForm />}

      <form
        className="grid gap-5 rounded-xl border border-border bg-card p-6 shadow-sm sm:grid-cols-2 xl:grid-cols-[minmax(0,1fr)_10rem_9rem_auto] xl:items-end"
        onSubmit={(event) => {
          event.preventDefault();
          setSearch(searchInput.trim());
          setPageNumber(1);
        }}
      >
        <div className="min-w-0 space-y-2">
          <Label htmlFor="supplier-search">Search by name or code</Label>
          <Input
            id="supplier-search"
            type="search"
            value={searchInput}
            className="h-11 bg-card"
            onChange={(event) => setSearchInput(event.target.value)}
            placeholder="For example, office"
          />
        </div>

        <div className="min-w-0 space-y-2">
          <Label htmlFor="supplier-status">Status</Label>
          <select
            id="supplier-status"
            value={status}
            onChange={(event) => {
              setStatus(event.target.value);
              setPageNumber(1);
            }}
            className="h-11 w-full rounded-lg border border-input bg-card px-3 text-sm shadow-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            <option value="all">All suppliers</option>
            <option value="active">Active</option>
            <option value="inactive">Inactive</option>
          </select>
        </div>

        <div className="min-w-0 space-y-2">
          <Label htmlFor="supplier-page-size">Rows per page</Label>
          <select
            id="supplier-page-size"
            value={pageSize}
            onChange={(event) => {
              setPageSize(Number(event.target.value));
              setPageNumber(1);
            }}
            className="h-11 w-full rounded-lg border border-input bg-card px-3 text-sm shadow-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            <option value={5}>5</option>
            <option value={10}>10</option>
            <option value={20}>20</option>
            <option value={50}>50</option>
          </select>
        </div>

        <Button type="submit" className="min-h-11 w-full self-end xl:w-auto">
          Search
        </Button>
      </form>

      {query.isPending ? (
        <FeedbackPanel title="Loading suppliers" role="status">
          <p>Fetching the supplier directory…</p>
        </FeedbackPanel>
      ) : query.isError ? (
        <FeedbackPanel title="Couldn't load suppliers" role="alert">
          <p>
            {query.error instanceof ApiError
              ? query.error.message
              : "Couldn't load suppliers. Check your connection and try again."}
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
        <FeedbackPanel title="No suppliers found">
          <p>
            No suppliers are available on this page with the current filters.
            Try another search, change the status filter, or return to an
            earlier page.
          </p>
        </FeedbackPanel>
      ) : (
        <div className="space-y-3">
          <p className="text-sm text-muted-foreground">
            Showing {query.data.items.length} of {query.data.totalCount}{" "}
            suppliers.
          </p>

          <SuppliersTable suppliers={query.data.items} />
        </div>
      )}

      {query.isSuccess && (
        <nav
          aria-label="Supplier pagination"
          className="flex flex-wrap items-center justify-between gap-4 rounded-xl border border-border bg-card px-5 py-4 shadow-sm"
        >
          <p className="text-sm text-muted-foreground" aria-live="polite">
            Page {pageNumber} of {totalPages}
          </p>

          <div className="flex gap-2">
            <Button
              type="button"
              variant="outline"
              className="min-h-11"
              disabled={pageNumber <= 1 || query.isFetching}
              onClick={() => setPageNumber((current) => current - 1)}
            >
              Previous
            </Button>

            <Button
              type="button"
              variant="outline"
              className="min-h-11"
              disabled={pageNumber >= totalPages || query.isFetching}
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
