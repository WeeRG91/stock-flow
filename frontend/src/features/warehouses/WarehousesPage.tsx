import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { ApiError } from "@/lib/ApiError";
import { useWarehouses } from "./useWarehouses";
import { StatusBadge } from "@/components/StatusBadge";
import { FeedbackPanel } from "@/components/FeedbackPanel";

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
        <FeedbackPanel title="Loading warehouses" role="status">
          <p>Fetching warehouse locations…</p>
        </FeedbackPanel>
      ) : query.isError ? (
        <FeedbackPanel title="Couldn't load warehouses" role="alert">
          <p>
            {query.error instanceof ApiError
              ? query.error.message
              : "Couldn't load warehouses. Check your connection and try again."}
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
      ) : warehouses.length === 0 ? (
        <FeedbackPanel title="No warehouses yet">
          <p>No warehouse locations are available.</p>
        </FeedbackPanel>
      ) : (
        <Card className="gap-0 py-0">
          <div className="flex items-center justify-between gap-3 border-b px-6 py-5">
            <h2 className="text-base font-semibold">Warehouse locations</h2>

            <span className="rounded-full bg-muted px-3 py-1 text-xs font-medium text-muted-foreground">
              {warehouses.length} warehouses
            </span>
          </div>

          <CardContent className="px-0">
            <div className="overflow-x-auto">
              <table className="w-full min-w-160 text-left text-sm">
                <caption className="sr-only">Warehouse locations</caption>

                <thead className="border-b bg-muted/50 text-xs text-muted-foreground">
                  <tr>
                    <th scope="col" className="px-6 py-3 font-medium">
                      Code
                    </th>
                    <th scope="col" className="px-6 py-3 font-medium">
                      Name
                    </th>
                    <th scope="col" className="px-6 py-3 font-medium">
                      Location
                    </th>
                    <th scope="col" className="px-6 py-3 font-medium">
                      Status
                    </th>
                  </tr>
                </thead>

                <tbody className="divide-y divide-border">
                  {warehouses.map((warehouse) => (
                    <tr key={warehouse.id} className="hover:bg-muted/30">
                      <td className="whitespace-nowrap px-6 py-4 font-mono text-xs text-muted-foreground">
                        {warehouse.code}
                      </td>

                      <td className="px-6 py-4 font-medium">
                        {warehouse.name}
                      </td>

                      <td className="px-6 py-4 text-muted-foreground">
                        {warehouse.location}
                      </td>

                      <td className="px-6 py-4">
                        <StatusBadge isActive={warehouse.isActive} />
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
