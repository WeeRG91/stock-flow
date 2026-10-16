import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { ApiError } from "@/lib/ApiError";
import { useProducts } from "./useProducts";

export function ProductsPage() {
  const query = useProducts();
  const products = query.data ?? [];

  return (
    <div className="space-y-6">
      <PageHeader title="Products" description="NovaTrade's product catalog." />

      {query.isPending ? (
        <p role="status">Loading products…</p>
      ) : query.isError ? (
        <div role="alert" className="space-y-3">
          <p>
            {query.error instanceof ApiError
              ? query.error.message
              : "Couldn't load products. Check your connection and try again."}
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
      ) : products.length === 0 ? (
        <p>No products are available yet.</p>
      ) : (
        <Card>
          <CardContent>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm">
                <caption className="sr-only">Product catalog</caption>
                <thead>
                  <tr className="border-b">
                    <th scope="col" className="px-3 py-3">
                      SKU
                    </th>
                    <th scope="col" className="px-3 py-3">
                      Name
                    </th>
                    <th scope="col" className="px-3 py-3">
                      Description
                    </th>
                    <th scope="col" className="px-3 py-3">
                      Status
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {products.map((product) => (
                    <tr key={product.id} className="border-b last:border-0">
                      <td className="whitespace-nowrap px-3 py-3 font-medium">
                        {product.sku}
                      </td>
                      <td className="px-3 py-3">{product.name}</td>
                      <td className="px-3 py-3 text-muted-foreground">
                        {product.description ?? "No description"}
                      </td>
                      <td className="px-3 py-3">
                        {product.isActive ? "Active" : "Inactive"}
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
