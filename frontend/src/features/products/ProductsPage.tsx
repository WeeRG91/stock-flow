import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { ApiError } from "@/lib/ApiError";
import { useProducts } from "./useProducts";
import { StatusBadge } from "@/components/StatusBadge";
import { FeedbackPanel } from "@/components/FeedbackPanel";

export function ProductsPage() {
  const query = useProducts();
  const products = query.data ?? [];

  return (
    <div className="space-y-6">
      <PageHeader title="Products" description="NovaTrade's product catalog." />

      {query.isPending ? (
        <FeedbackPanel title="Loading products" role="status">
          <p>Fetching the product catalog…</p>
        </FeedbackPanel>
      ) : query.isError ? (
        <FeedbackPanel title="Couldn't load products" role="alert">
          <p>
            {query.error instanceof ApiError
              ? query.error.message
              : "Couldn't load products. Check your connection and try again."}
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
      ) : products.length === 0 ? (
        <FeedbackPanel title="No products yet">
          <p>No products are available in the catalog.</p>
        </FeedbackPanel>
      ) : (
        <Card className="gap-0 py-0">
          <div className="flex items-center justify-between gap-3 border-b px-6 py-5">
            <h2 className="text-base font-semibold">Product catalog</h2>

            <span className="rounded-full bg-muted px-3 py-1 text-xs font-medium text-muted-foreground">
              {products.length} products
            </span>
          </div>

          <CardContent className="px-0">
            <div className="overflow-x-auto">
              <table className="w-full min-w-160 text-left text-sm">
                <caption className="sr-only">Product catalog</caption>

                <thead className="border-b bg-muted/50 text-xs text-muted-foreground">
                  <tr>
                    <th scope="col" className="px-6 py-3 font-medium">
                      SKU
                    </th>
                    <th scope="col" className="px-6 py-3 font-medium">
                      Name
                    </th>
                    <th scope="col" className="px-6 py-3 font-medium">
                      Description
                    </th>
                    <th scope="col" className="px-6 py-3 font-medium">
                      Status
                    </th>
                  </tr>
                </thead>

                <tbody className="divide-y divide-border">
                  {products.map((product) => (
                    <tr key={product.id} className="hover:bg-muted/30">
                      <td className="whitespace-nowrap px-6 py-4 font-mono text-xs text-muted-foreground">
                        {product.sku}
                      </td>

                      <td className="px-6 py-4 font-medium">{product.name}</td>

                      <td className="max-w-80 px-6 py-4 leading-6 text-muted-foreground wrap-break-word">
                        {product.description ?? "No description"}
                      </td>

                      <td className="px-6 py-4">
                        <StatusBadge isActive={product.isActive} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
