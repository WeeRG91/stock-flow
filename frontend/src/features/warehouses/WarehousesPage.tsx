import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { ApiError } from "@/lib/ApiError";
import { useWarehouses } from "./useWarehouses";

export function WarehousesPage() {
  const query = useWarehouses();
  const warehouses = query.data ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        title="Warehouses"
        description="NovaTrade's warehouse locations."
      />

      {query.isPending ? (
        <p role="status">Loading warehouses…</p>
      ) : query.isError ? (
        <div role="alert" className="space-y-3">
          <p>
            {query.error instanceof ApiError
              ? query.error.message
              : "Couldn't load warehouses. Check your connection and try again."}
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
      ) : warehouses.length === 0 ? (
        <p>No warehouses are available yet.</p>
      ) : (
        <Card>
          <CardContent>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm">
                <caption className="sr-only">Warehouse locations</caption>
                <thead>
                  <tr className="border-b">
                    <th scope="col" className="px-3 py-3">
                      Code
                    </th>
                    <th scope="col" className="px-3 py-3">
                      Name
                    </th>
                    <th scope="col" className="px-3 py-3">
                      Location
                    </th>
                    <th scope="col" className="px-3 py-3">
                      Status
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {warehouses.map((warehouse) => (
                    <tr key={warehouse.id} className="border-b last:border-0">
                      <td className="whitespace-nowrap px-3 py-3 font-medium">
                        {warehouse.code}
                      </td>
                      <td className="px-3 py-3">{warehouse.name}</td>
                      <td className="px-3 py-3">{warehouse.location}</td>
                      <td className="px-3 py-3">
                        {warehouse.isActive ? "Active" : "Inactive"}
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
