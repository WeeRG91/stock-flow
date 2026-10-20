import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/ApiError";
import { useSuppliers } from "./useSuppliers";
import { SuppliersTable } from "./SuppliersTable";
import { useState } from "react";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { CreateSupplierForm } from "./CreateSupplierForm";

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

  const totalPages = query.data
    ? Math.max(1, Math.ceil(query.data.totalCount / pageSize))
    : 1;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Suppliers"
        description="Manage NovaTrade's suppliers and contact information."
      />

      <CreateSupplierForm />

      <form
        className="flex flex-col gap-3 sm:flex-row sm:items-end"
        onSubmit={(event) => {
          event.preventDefault();
          setSearch(searchInput.trim());
          setPageNumber(1);
        }}
      >
        <div className="w-full space-y-2 sm:max-w-sm">
          <Label htmlFor="supplier-search">Search by name or code</Label>
          <Input
            id="supplier-search"
            type="search"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            placeholder="For example, office"
          />
        </div>

        <div className="w-full space-y-2 sm:w-40">
          <Label htmlFor="supplier-status">Status</Label>
          <select
            id="supplier-status"
            value={status}
            onChange={(event) => {
              setStatus(event.target.value);
              setPageNumber(1);
            }}
            className="h-8 w-full rounded-lg border border-input bg-background px-2.5 text-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            <option value="all">All suppliers</option>
            <option value="active">Active</option>
            <option value="inactive">Inactive</option>
          </select>
        </div>

        <div className="w-full space-y-2 sm:w-32">
          <Label htmlFor="supplier-page-size">Rows per page</Label>
          <select
            id="supplier-page-size"
            value={pageSize}
            onChange={(event) => {
              setPageSize(Number(event.target.value));
              setPageNumber(1);
            }}
            className="h-8 w-full rounded-lg border border-input bg-background px-2.5 text-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            <option value={5}>5</option>
            <option value={10}>10</option>
            <option value={20}>20</option>
            <option value={50}>50</option>
          </select>
        </div>

        <Button type="submit">Search</Button>
      </form>

      {query.isPending ? (
        <p role="status">Loading suppliers…</p>
      ) : query.isError ? (
        <div role="alert" className="space-y-3">
          <p>
            {query.error instanceof ApiError
              ? query.error.message
              : "Couldn't load suppliers. Check your connection and try again."}
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
        <p>No suppliers match your filters.</p>
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
          className="flex flex-wrap items-center gap-3"
        >
          <Button
            type="button"
            variant="outline"
            disabled={pageNumber <= 1 || query.isFetching}
            onClick={() => setPageNumber((current) => current - 1)}
          >
            Previous
          </Button>

          <span className="text-sm">
            Page {pageNumber} of {totalPages}
          </span>

          <Button
            type="button"
            variant="outline"
            disabled={pageNumber >= totalPages || query.isFetching}
            onClick={() => setPageNumber((current) => current + 1)}
          >
            Next
          </Button>
        </nav>
      )}
    </div>
  );
}
