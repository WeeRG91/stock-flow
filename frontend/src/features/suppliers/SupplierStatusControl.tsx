import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/ApiError";
import type { SupplierDto } from "@/types/api";
import { useSetSupplierStatus } from "./useSetSupplierStatus";

type SupplierStatusControlProps = {
  supplier: SupplierDto;
};

export function SupplierStatusControl({
  supplier,
}: SupplierStatusControlProps) {
  const mutation = useSetSupplierStatus(supplier.id);

  return (
    <section
      aria-labelledby="supplier-status-heading"
      className="space-y-4 rounded-xl border bg-card p-6"
    >
      <h2 id="supplier-status-heading" className="text-lg font-semibold">
        Supplier status
      </h2>

      <p className="text-sm text-muted-foreground">
        Deactivating a supplier preserves its record and contact information.
      </p>

      <Button
        type="button"
        variant="outline"
        disabled={mutation.isPending}
        onClick={() => {
          if (mutation.isPending) return;
          mutation.mutate(!supplier.isActive);
        }}
      >
        {mutation.isPending
          ? "Updating status…"
          : supplier.isActive
            ? "Deactivate supplier"
            : "Activate supplier"}
      </Button>

      {mutation.isSuccess && (
        <p role="status" className="text-sm">
          Supplier {mutation.data.isActive ? "activated" : "deactivated"} successfully.
        </p>
      )}

      {mutation.isError && (
        <p role="alert" className="text-sm text-destructive">
          {mutation.error instanceof ApiError
            ? mutation.error.message
            : "Couldn't confirm the status change. Refresh supplier details before trying again."}
        </p>
      )}
    </section>
  );
}