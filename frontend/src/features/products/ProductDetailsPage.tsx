import { Link, useParams } from "react-router";
import { PageHeader } from "@/components/PageHeader";
import { FeedbackPanel } from "@/components/FeedbackPanel";
import { StatusBadge } from "@/components/StatusBadge";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/ApiError";
import { useProduct } from "./useProduct";
import { FilteredInventoryBalances } from "../inventory/FilteredInventoryBalance";
import { usePermissions } from "../auth/usePermissions";

export function ProductDetailsPage() {
  const { id } = useParams<{ id: string }>();
  const query = useProduct(id ?? "");
  const { can } = usePermissions();

  const isNotFound =
    query.error instanceof ApiError && query.error.status === 404;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Product details"
        description="View product information and warehouse stock balances."
      />

      <Link
        to="/products"
        className="inline-flex min-h-11 items-center rounded-lg text-sm font-medium text-primary underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-4"
      >
        Back to products
      </Link>

      {!id ? (
        <FeedbackPanel title="Missing product identifier" role="alert">
          <p>A product identifier is required.</p>
        </FeedbackPanel>
      ) : query.isPending ? (
        <FeedbackPanel title="Loading product" role="status">
          <p>Fetching product information…</p>
        </FeedbackPanel>
      ) : query.isError ? (
        <FeedbackPanel
          title={isNotFound ? "Product not found" : "Couldn't load product"}
          role="alert"
        >
          <p>
            {isNotFound
              ? "Return to the product catalog to select another product."
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
            aria-labelledby="product-heading"
            className="space-y-4 rounded-xl border bg-card p-6 shadow-sm"
          >
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div className="min-w-0">
                <h2
                  id="product-heading"
                  className="text-lg font-semibold wrap-break-word"
                >
                  {query.data.name}
                </h2>
                <p className="mt-1 font-mono text-sm text-muted-foreground wrap-break-word">
                  {query.data.sku}
                </p>
              </div>

              <StatusBadge isActive={query.data.isActive} />
            </div>

            <p className="text-sm leading-6 text-muted-foreground wrap-break-word">
              {query.data.description ?? "No description available."}
            </p>
          </section>

          {can("Inventory.Read") && (
            <FilteredInventoryBalances
              key={query.data.id}
              filters={{ productId: query.data.id }}
            />
          )}
        </>
      )}
    </div>
  );
}
