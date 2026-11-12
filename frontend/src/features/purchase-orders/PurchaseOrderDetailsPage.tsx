import { Link, useParams } from "react-router";
import { FeedbackPanel } from "@/components/FeedbackPanel";
import { PageHeader } from "@/components/PageHeader";
import { ApiError } from "@/lib/ApiError";
import type { PurchaseOrderDetailsDto, PurchaseOrderStatus } from "@/types/api";
import { usePurchaseOrder } from "./usePurchaseOrder";
import { useProducts } from "../products/useProducts";
import { Button } from "@/components/ui/button";
import { EditDraftPurchaseOrderForm } from "./EditDraftPurchaseOrderForm";
import { useState } from "react";
import { usePermissions } from "../auth/usePermissions";
import { useSubmitPurchaseOrder } from "./useSubmitPurchaseOrder";
import { useDecidePurchaseOrder } from "./useDecidePurchaseOrder";

const statusLabels: Record<PurchaseOrderStatus, string> = {
  1: "Draft",
  2: "Pending approval",
  3: "Approved",
  4: "Rejected",
  5: "Partially received",
  6: "Received",
};

export function PurchaseOrderDetailsPage() {
  const { id } = useParams<{ id: string }>();
  const query = usePurchaseOrder(id ?? "");
  const isNotFound =
    query.error instanceof ApiError && query.error.status === 404;
  const { can, user } = usePermissions();

  return (
    <div className="space-y-6">
      <PageHeader
        title={query.data?.orderNumber ?? "Purchase order details"}
        description="Review the supplier, warehouse, and status for this order."
      />

      <Link
        to="/purchase-orders"
        className="inline-flex min-h-11 items-center rounded-lg text-sm font-medium text-primary underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-4"
      >
        Back to purchase orders
      </Link>

      {!id ? (
        <FeedbackPanel title="Missing order identifier" role="alert">
          <p>Open an order from the purchase orders list.</p>
        </FeedbackPanel>
      ) : query.isPending ? (
        <FeedbackPanel title="Loading purchase order" role="status">
          <p>Fetching order details.</p>
        </FeedbackPanel>
      ) : query.isError ? (
        <FeedbackPanel
          title={
            isNotFound
              ? "Purchase order not found"
              : "Couldn't load purchase order"
          }
          role="alert"
        >
          <p>
            {isNotFound
              ? "Choose another order from the list."
              : "Try refreshing the page."}
          </p>
        </FeedbackPanel>
      ) : (
        <>
          <dl className="grid gap-4 rounded-xl border bg-card p-6 sm:grid-cols-3">
            <div>
              <dt className="text-sm text-muted-foreground">Supplier</dt>
              <dd className="font-medium">{query.data.supplierName}</dd>
            </div>
            <div>
              <dt className="text-sm text-muted-foreground">Warehouse</dt>
              <dd className="font-medium">{query.data.warehouseName}</dd>
            </div>
            <div>
              <dt className="text-sm text-muted-foreground">Status</dt>
              <dd className="font-medium">{statusLabels[query.data.status]}</dd>
            </div>
          </dl>

          <PurchaseOrderLines order={query.data} />

          {query.data?.status === 1 && can("PurchaseOrders.Manage") && (
            <DraftLineEditor key={query.data.id} order={query.data} />
          )}

          {query.data.status === 2 && can("PurchaseOrders.Approve") && (
            <PurchaseOrderDecision
              key={query.data.id}
              order={query.data}
              isCreator={user.id === query.data.createdByUserId}
            />
          )}
        </>
      )}
    </div>
  );
}

function DraftLineEditor({ order }: { order: PurchaseOrderDetailsDto }) {
  const [isEditing, setIsEditing] = useState(false);
  const submission = useSubmitPurchaseOrder(order.id);
  const hasLines = order.lines.length > 0;

  if (!isEditing) {
    return (
      <div className="space-y-3">
        <div className="flex flex-wrap gap-3">
          <Button
            type="button"
            variant="outline"
            disabled={submission.isPending}
            onClick={() => {
              submission.reset();
              setIsEditing(true);
            }}
          >
            Edit draft lines
          </Button>

          <Button
            type="button"
            disabled={!hasLines || submission.isPending}
            onClick={() => submission.mutate(order.version)}
          >
            {submission.isPending ? "Submitting…" : "Submit for approval"}
          </Button>
        </div>

        {!hasLines && (
          <p className="text-sm text-muted-foreground">
            Save at least one line before submitting this order.
          </p>
        )}

        {submission.isError && (
          <p role="alert" className="text-sm text-destructive">
            {submission.error instanceof ApiError &&
            submission.error.status === 409
              ? "The order may have changed. Refresh the page and review it before submitting again."
              : submission.error.message}
          </p>
        )}
      </div>
    );
  }

  return (
    <DraftLineEditorContent order={order} onClose={() => setIsEditing(false)} />
  );
}

function DraftLineEditorContent({
  order,
  onClose,
}: {
  order: PurchaseOrderDetailsDto;
  onClose: () => void;
}) {
  const products = useProducts();

  return (
    <section className="space-y-4" aria-label="Draft line editor">
      <Button type="button" variant="outline" onClick={onClose}>
        Close editor
      </Button>

      {products.isPending ? (
        <FeedbackPanel title="Loading products" role="status">
          <p>Fetching the product catalog.</p>
        </FeedbackPanel>
      ) : products.isError ? (
        <FeedbackPanel title="Couldn't load products" role="alert">
          <p>Refresh the page to try again.</p>
        </FeedbackPanel>
      ) : (
        <EditDraftPurchaseOrderForm
          order={order}
          products={products.data}
          onSaved={onClose}
        />
      )}
    </section>
  );
}

function PurchaseOrderLines({ order }: { order: PurchaseOrderDetailsDto }) {
  const currency = new Intl.NumberFormat(undefined, {
    style: "currency",
    currency: order.currency,
  });

  return (
    <section className="space-y-4 rounded-xl border bg-card p-6">
      <h2 className="font-semibold">Order lines</h2>

      {order.lines.length === 0 ? (
        <p className="text-sm text-muted-foreground">No lines added yet.</p>
      ) : (
        <ul className="divide-y">
          {order.lines.map((line) => (
            <li
              key={line.id}
              className="flex flex-wrap items-start justify-between gap-2 py-3"
            >
              <div>
                <p className="font-medium">{line.productName}</p>
                <p className="text-sm text-muted-foreground">
                  {line.productSku} · {line.orderedQuantity} ×{" "}
                  {currency.format(line.unitPrice)}
                </p>
              </div>
              <p className="font-medium">{currency.format(line.lineTotal)}</p>
            </li>
          ))}
        </ul>
      )}

      <p className="border-t pt-4 text-right font-semibold">
        Order total {currency.format(order.totalAmount)}
      </p>
    </section>
  );
}

function PurchaseOrderDecision({
  order,
  isCreator,
}: {
  order: PurchaseOrderDetailsDto;
  isCreator: boolean;
}) {
  const mutation = useDecidePurchaseOrder(order.id);

  return (
    <section className="space-y-3" aria-label="Purchase order decision">
      <div className="flex flex-wrap gap-3">
        <Button
          type="button"
          disabled={isCreator || mutation.isPending}
          onClick={() =>
            mutation.mutate({ decision: "approve", expectedVersion: order.version })
          }
        >
          Approve
        </Button>
        <Button
          type="button"
          variant="outline"
          disabled={mutation.isPending}
          onClick={() =>
            mutation.mutate({ decision: "reject", expectedVersion: order.version })
          }
        >
          Reject
        </Button>
      </div>

      {isCreator && (
        <p className="text-sm text-muted-foreground">
          Another approver must approve an order you created.
        </p>
      )}

      {mutation.isPending && <p role="status">Saving decision…</p>}

      {mutation.isError && (
        <p role="alert" className="text-sm text-destructive">
          {mutation.error instanceof ApiError && mutation.error.status === 409
            ? "The order may have changed. Refresh it and review its status before trying again."
            : mutation.error.message}
        </p>
      )}
    </section>
  );
}
