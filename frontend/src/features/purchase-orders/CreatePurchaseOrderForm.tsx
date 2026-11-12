import { zodResolver } from "@hookform/resolvers/zod";
import { useId } from "react";
import { useForm } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import type { SupplierDto, WarehouseDto } from "@/types/api";
import {
  createPurchaseOrderSchema,
  type CreatePurchaseOrderFormValues,
} from "./createPurchaseOrderSchema";
import { useCreatePurchaseOrder } from "./useCreatePurchaseOrder";

type CreatePurchaseOrderFormProps = {
  suppliers: readonly SupplierDto[];
  warehouses: readonly WarehouseDto[];
};

const selectClassName =
  "h-11 w-full rounded-lg border border-input bg-card px-3 text-sm " +
  "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring " +
  "aria-invalid:border-destructive";

export function CreatePurchaseOrderForm({
  suppliers,
  warehouses,
}: CreatePurchaseOrderFormProps) {
  const formId = useId();
  const mutation = useCreatePurchaseOrder();
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<CreatePurchaseOrderFormValues>({
    resolver: zodResolver(createPurchaseOrderSchema),
    defaultValues: { supplierId: "", warehouseId: "" },
  });

  function onSubmit(values: CreatePurchaseOrderFormValues) {
    if (mutation.isPending) return;

    mutation.mutate({ ...values, lines: [] }, {
      onSuccess: () => reset(),
    });
  }

  return (
    <form
      aria-labelledby={`${formId}-heading`}
      className="space-y-4 rounded-xl border bg-card p-6 shadow-sm"
      onSubmit={handleSubmit(onSubmit)}
      noValidate
    >
      <h2 id={`${formId}-heading`} className="font-semibold">
        Create purchase order draft
      </h2>

      <fieldset disabled={mutation.isPending} className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor={`${formId}-supplier`}>Supplier</Label>
          <select
            id={`${formId}-supplier`}
            className={selectClassName}
            aria-invalid={!!errors.supplierId}
            aria-describedby={errors.supplierId ? `${formId}-supplier-error` : undefined}
            {...register("supplierId")}
          >
            <option value="">Select a supplier</option>
            {suppliers.map((supplier) => (
              <option key={supplier.id} value={supplier.id}>
                {supplier.code} — {supplier.name}
              </option>
            ))}
          </select>
          {errors.supplierId && (
            <p id={`${formId}-supplier-error`} className="text-sm text-destructive">
              {errors.supplierId.message}
            </p>
          )}
        </div>

        <div className="space-y-2">
          <Label htmlFor={`${formId}-warehouse`}>Warehouse</Label>
          <select
            id={`${formId}-warehouse`}
            className={selectClassName}
            aria-invalid={!!errors.warehouseId}
            aria-describedby={errors.warehouseId ? `${formId}-warehouse-error` : undefined}
            {...register("warehouseId")}
          >
            <option value="">Select a warehouse</option>
            {warehouses.map((warehouse) => (
              <option key={warehouse.id} value={warehouse.id}>
                {warehouse.code} — {warehouse.name}
              </option>
            ))}
          </select>
          {errors.warehouseId && (
            <p id={`${formId}-warehouse-error`} className="text-sm text-destructive">
              {errors.warehouseId.message}
            </p>
          )}
        </div>
      </fieldset>

      {mutation.isError && (
        <p role="alert" className="text-sm text-destructive">
          {mutation.error.message}
        </p>
      )}

      {mutation.isSuccess && (
        <p role="status" className="text-sm">
          Draft {mutation.data.orderNumber} created.
        </p>
      )}

      <Button type="submit" disabled={mutation.isPending}>
        {mutation.isPending ? "Creating draft…" : "Create draft"}
      </Button>
    </form>
  );
}