import { FeedbackPanel } from "@/components/FeedbackPanel";
import { Button } from "@/components/ui/button";
import { useProducts } from "@/features/products/useProducts";
import { useWarehouses } from "@/features/warehouses/useWarehouses";
import { CreateStockReservationForm } from "./CreateStockReservationForm";

export function CreateStockReservationSection() {
  const products = useProducts();
  const warehouses = useWarehouses();

  if (products.isPending || warehouses.isPending) {
    return (
      <FeedbackPanel title="Preparing stock reservation" role="status">
        <p>Loading products and warehouses…</p>
      </FeedbackPanel>
    );
  }

  if (products.isError || warehouses.isError) {
    return (
      <FeedbackPanel title="Reservation form unavailable" role="alert">
        <p>Couldn't load products or warehouses.</p>
        <Button
          type="button"
          variant="outline"
          disabled={products.isFetching || warehouses.isFetching}
          onClick={() => {
            void products.refetch();
            void warehouses.refetch();
          }}
        >
          Try again
        </Button>
      </FeedbackPanel>
    );
  }

  if (
    !products.data.some((product) => product.isActive) ||
    !warehouses.data.some((warehouse) => warehouse.isActive)
  ) {
    return (
      <FeedbackPanel title="Active catalog entries required">
        <p>
          Reservations require an active product and an active warehouse.
        </p>
      </FeedbackPanel>
    );
  }

  return (
    <CreateStockReservationForm
      products={products.data}
      warehouses={warehouses.data}
    />
  );
}