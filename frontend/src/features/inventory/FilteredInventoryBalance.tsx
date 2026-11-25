import { useState } from "react";
import { Button } from "@/components/ui/button";
import { FeedbackPanel } from "@/components/FeedbackPanel";
import type { InventoryFilters } from "./api";
import { InventoryTable } from "./InventoryTable";
import { useInventory } from "./useInventory";

type Props = {
  filters: InventoryFilters;
};

const pageSize = 20;

export function FilteredInventoryBalances({ filters }: Props) {
  const [pageNumber, setPageNumber] = useState(1);
  const query = useInventory(pageNumber, pageSize, filters);

  const totalPages = Math.max(
    1,
    Math.ceil((query.data?.totalCount ?? 0) / pageSize),
  );

  return (
    <section aria-label="Inventory balances" className="space-y-4">
      {query.isPending ? (
        <FeedbackPanel title="Loading balances" role="status">
          <p>Fetching current stock balances…</p>
        </FeedbackPanel>
      ) : query.isError ? (
        <FeedbackPanel title="Couldn't load balances" role="alert">
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
        <FeedbackPanel title="No balances to display">
          <p>
            {query.data.totalCount === 0
              ? "No inventory balances have been recorded for this selection."
              : "This page is empty. Return to a previous page."}
          </p>
        </FeedbackPanel>
      ) : (
        <InventoryTable items={query.data.items} />
      )}

      <nav
        aria-label="Stock balance pagination"
        className="flex flex-wrap items-center justify-between gap-3"
      >
        <Button
          type="button"
          variant="outline"
          disabled={pageNumber <= 1 || query.isFetching}
          onClick={() => setPageNumber((page) => page - 1)}
        >
          Previous
        </Button>

        <p role="status" className="text-sm text-muted-foreground">
          Page {pageNumber}
          {query.isSuccess &&
            ` of ${totalPages} · ${query.data.totalCount} balances`}
        </p>

        <Button
          type="button"
          variant="outline"
          disabled={
            !query.isSuccess ||
            pageNumber >= totalPages ||
            query.isFetching
          }
          onClick={() => setPageNumber((page) => page + 1)}
        >
          Next
        </Button>
      </nav>
    </section>
  );
}