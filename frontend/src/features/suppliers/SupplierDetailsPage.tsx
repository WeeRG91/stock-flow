import { Link, useParams } from "react-router";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { ApiError } from "@/lib/ApiError";
import { useSupplier } from "./useSupplier";
import { UpdateSupplierForm } from "./UpdateSupplierForm";
import { SupplierStatusControl } from "./SupplierStatusControl";
import { StatusBadge } from "@/components/StatusBadge";
import { FeedbackPanel } from "@/components/FeedbackPanel";
import { usePermissions } from "../auth/usePermissions";

export function SupplierDetailsPage() {
  const { id } = useParams<{ id: string }>();
  const query = useSupplier(id ?? "");
  const { can } = usePermissions();
  const canManageSuppliers = can("Suppliers.Manage");

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
        className="inline-flex min-h-11 items-center rounded-lg text-sm font-medium text-primary underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring"
      >
        Back to suppliers
      </Link>

      {!id ? (
        <FeedbackPanel title="Missing supplier identifier" role="alert">
          <p>A supplier identifier is required.</p>
        </FeedbackPanel>
      ) : query.isPending ? (
        <FeedbackPanel title="Loading supplier" role="status">
          <p>Fetching supplier contact information…</p>
        </FeedbackPanel>
      ) : query.isError ? (
        <FeedbackPanel
          title={isNotFound ? "Supplier not found" : "Couldn't load supplier"}
          role="alert"
        >
          <p>
            {isNotFound
              ? "This supplier could not be found. Return to the supplier directory to select another."
              : query.error instanceof ApiError
                ? query.error.message
                : "Couldn't load this supplier. Check your connection and try again."}
          </p>

          {!isNotFound && (
            <Button
              type="button"
              variant="outline"
              className="min-h-11"
              disabled={query.isFetching}
              onClick={() => void query.refetch()}
            >
              Try again
            </Button>
          )}
        </FeedbackPanel>
      ) : (
        <div className="space-y-6 mt-2">
          <Card className="gap-0 py-0">
            <div className="flex flex-wrap items-center justify-between gap-3 border-b px-6 py-5">
              <div className="min-w-0">
                <h2 className="text-base font-semibold wrap-break-word">
                  {query.data.name}
                </h2>
                <p className="mt-1 font-mono text-xs text-muted-foreground wrap-break-word">
                  {query.data.code}
                </p>
              </div>

              <StatusBadge isActive={query.data.isActive} />
            </div>

            <CardContent className="py-6">
              <dl className="grid gap-6 sm:grid-cols-2">
                <div className="min-w-0">
                  <dt className="text-xs font-medium text-muted-foreground">
                    Contact email
                  </dt>
                  <dd className="mt-2 text-sm font-medium wrap-break-word">
                    {query.data.contactEmail}
                  </dd>
                </div>

                <div className="min-w-0">
                  <dt className="text-xs font-medium text-muted-foreground">
                    Phone number
                  </dt>
                  <dd className="mt-2 text-sm font-medium wrap-break-word">
                    {query.data.phoneNumber ?? "Not provided"}
                  </dd>
                </div>
              </dl>
            </CardContent>
          </Card>

          {canManageSuppliers && (
            <>
              <SupplierStatusControl
                key={`status-${query.data.id}`}
                supplier={query.data}
              />

              <UpdateSupplierForm key={query.data.id} supplier={query.data} />
            </>
          )}
        </div>
      )}
    </div>
  );
}
