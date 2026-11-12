import { PageHeader } from "@/components/PageHeader";
import { usePurchaseOrders } from "./usePurchaseOrders";
import { FeedbackPanel } from "@/components/FeedbackPanel";
import { usePermissions } from "../auth/usePermissions";
import { CreatePurchaseOrderForm } from "./CreatePurchaseOrderForm";
import { useSuppliers } from "../suppliers/useSuppliers";
import { useWarehouses } from "../warehouses/useWarehouses";
import { Link } from "react-router";

export function PurchaseOrdersPage() {
  const query = usePurchaseOrders({ pageNumber: 1, pageSize: 20 });
  const { can } = usePermissions();

  return (
    <div className="space-y-6">
      <PageHeader
        title="Purchase Orders"
        description="Review NovaTrade’s purchase orders."
      />

      {can("PurchaseOrders.Manage") && <CreatePurchaseOrderSection />}

      {query.isPending ? (
        <FeedbackPanel title="Loading purchase orders" role="status">
          <p>Fetching the first page.</p>
        </FeedbackPanel>
      ) : query.isError ? (
        <FeedbackPanel title="Couldn't load purchase orders" role="alert">
          <p>Try refreshing the page.</p>
        </FeedbackPanel>
      ) : query.data.items.length === 0 ? (
        <FeedbackPanel title="No purchase orders yet">
          <p>New purchase orders will appear here.</p>
        </FeedbackPanel>
      ) : (
        <ul className="divide-y rounded-xl border bg-card">
          {query.data.items.map((order) => (
            <li key={order.id} className="px-6 py-4">
              <Link
                to={`/purchase-orders/${order.id}`}
                className="break-all rounded-sm font-medium text-primary underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-2"
              >
                {order.orderNumber}
              </Link>
              <p className="text-sm text-muted-foreground">
                {order.supplierName}
              </p>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function CreatePurchaseOrderSection() {
  const suppliers = useSuppliers({
    pageNumber: 1,
    pageSize: 100,
    isActive: true,
  });
  const warehouses = useWarehouses();

  if (suppliers.isError || warehouses.isError) {
    return (
      <FeedbackPanel title="Couldn't load draft options" role="alert">
        <p>Refresh the page to try again.</p>
      </FeedbackPanel>
    );
  }

  if (suppliers.isPending || warehouses.isPending) {
    return (
      <FeedbackPanel title="Loading draft options" role="status">
        <p>Fetching suppliers and warehouses.</p>
      </FeedbackPanel>
    );
  }

  const activeWarehouses = warehouses.data.filter(
    (warehouse) => warehouse.isActive,
  );

  if (suppliers.data.items.length === 0 || activeWarehouses.length === 0) {
    return (
      <FeedbackPanel title="Draft creation is unavailable">
        <p>You need at least one active supplier and one active warehouse.</p>
      </FeedbackPanel>
    );
  }

  return (
    <CreatePurchaseOrderForm
      suppliers={suppliers.data.items}
      warehouses={activeWarehouses}
    />
  );
}
