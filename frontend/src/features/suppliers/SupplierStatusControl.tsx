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
      className="space-y-5 rounded-xl border border-border bg-card p-6 shadow-sm"
    >
      <div className="space-y-1">
        <h2 id="supplier-status-heading" className="text-base font-semibold">
          Supplier status
        </h2>

        <p className="text-sm leading-6 text-muted-foreground">
          Deactivating a supplier preserves its record and contact information.
        </p>
      </div>

      <Button
        type="button"
        variant="outline"
        className="min-h-11 w-full sm:w-auto"
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
        <p role="status" className="rounded-lg border border-primary/20 bg-accent px-4 py-3 text-sm text-accent-foreground">
          Supplier {mutation.data.isActive ? "activated" : "deactivated"}{" "}
          successfully.
        </p>
      )}

      {mutation.isError && (
        <p role="alert" className="rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
          {mutation.error instanceof ApiError
            ? mutation.error.message
            : "Couldn't confirm the status change. Refresh supplier details before trying again."}
        </p>
      )}
    </section>
  );
}
