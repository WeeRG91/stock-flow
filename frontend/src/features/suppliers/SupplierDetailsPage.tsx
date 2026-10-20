import { Link, useParams } from "react-router";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { ApiError } from "@/lib/ApiError";
import { useSupplier } from "./useSupplier";
import { UpdateSupplierForm } from "./UpdateSupplierForm";
import { SupplierStatusControl } from "./SupplierStatusControl";

export function SupplierDetailsPage() {
  const { id } = useParams<{ id: string }>();
  const query = useSupplier(id ?? "");

  const isNotFound =
    query.error instanceof ApiError && query.error.status === 404;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Supplier details"
        description="View supplier contact information and status."
      />

      <Link
        to="/suppliers"
        className="text-sm font-medium underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-4"
      >
        Back to suppliers
      </Link>

      {!id ? (
        <p role="alert">A supplier identifier is required.</p>
      ) : query.isPending ? (
        <p role="status">Loading supplier…</p>
      ) : query.isError ? (
        <div role="alert" className="space-y-3">
          <p>
            {isNotFound
              ? "This supplier could not be found."
              : query.error instanceof ApiError
                ? query.error.message
                : "Couldn't load this supplier. Check your connection and try again."}
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
        </div>
      ) : (
        <div className="space-y-6 mt-2">
          <Card>
            <CardContent>
              <dl className="grid gap-5 sm:grid-cols-2">
                <div>
                  <dt className="text-sm text-muted-foreground">Code</dt>
                  <dd className="mt-1 font-medium">{query.data.code}</dd>
                </div>

                <div>
                  <dt className="text-sm text-muted-foreground">Name</dt>
                  <dd className="mt-1 font-medium">{query.data.name}</dd>
                </div>

                <div>
                  <dt className="text-sm text-muted-foreground">Email</dt>
                  <dd className="mt-1 wrap-break-word">
                    {query.data.contactEmail}
                  </dd>
                </div>

                <div>
                  <dt className="text-sm text-muted-foreground">Phone</dt>
                  <dd className="mt-1">
                    {query.data.phoneNumber ?? "Not provided"}
                  </dd>
                </div>

                <div>
                  <dt className="text-sm text-muted-foreground">Status</dt>
                  <dd className="mt-1">
                    {query.data.isActive ? "Active" : "Inactive"}
                  </dd>
                </div>
              </dl>
            </CardContent>
          </Card>

          <SupplierStatusControl
            key={`status-${query.data.id}`}
            supplier={query.data}
          />

          <UpdateSupplierForm key={query.data.id} supplier={query.data} />
        </div>
      )}
    </div>
  );
}
