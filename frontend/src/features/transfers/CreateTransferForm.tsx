import { zodResolver } from "@hookform/resolvers/zod";
import { useId } from "react";
import { useForm } from "react-hook-form";
import { Link } from "react-router";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import type { WarehouseDto } from "@/types/api";
import {
  createTransferSchema,
  type CreateTransferFormValues,
} from "./createTransferSchema";
import { useCreateTransfer } from "./useCreateTransfer";

const warehouseFields = [
  { name: "sourceWarehouseId", label: "Source warehouse" },
  { name: "destinationWarehouseId", label: "Destination warehouse" },
] as const;

export function CreateTransferForm({
  warehouses,
}: {
  warehouses: readonly WarehouseDto[];
}) {
  const formId = useId();
  const mutation = useCreateTransfer();

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<CreateTransferFormValues>({
    resolver: zodResolver(createTransferSchema),
    defaultValues: {
      sourceWarehouseId: "",
      destinationWarehouseId: "",
      lines: [],
    },
  });

  const busy = isSubmitting || mutation.isPending;
  const activeWarehouses = warehouses.filter((warehouse) => warehouse.isActive);

  async function onSubmit(values: CreateTransferFormValues) {
    if (mutation.isPending) return;

    try {
      await mutation.mutateAsync(values);
      reset();
    } catch {
      // The mutation exposes the error for display below.
    }
  }

  return (
    <form
      aria-labelledby={`${formId}-heading`}
      className="space-y-4 rounded-xl border bg-card p-6 shadow-sm"
      onSubmit={handleSubmit(onSubmit)}
      noValidate
    >
      <h2 id={`${formId}-heading`} className="font-semibold">
        Create transfer draft
      </h2>

      <fieldset disabled={busy} className="grid gap-4 sm:grid-cols-2">
        {warehouseFields.map(({ name, label }) => {
          const error = errors[name];
          const inputId = `${formId}-${name}`;

          return (
            <div key={name} className="space-y-2">
              <Label htmlFor={inputId}>{label}</Label>

              <select
                id={inputId}
                className="h-11 w-full rounded-lg border border-input bg-card px-3 text-sm aria-invalid:border-destructive focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                aria-invalid={!!error}
                aria-describedby={error ? `${inputId}-error` : undefined}
                {...register(name)}
              >
                <option value="">Select a warehouse</option>
                {activeWarehouses.map((warehouse) => (
                  <option key={warehouse.id} value={warehouse.id}>
                    {warehouse.code} — {warehouse.name}
                  </option>
                ))}
              </select>

              {error && (
                <p id={`${inputId}-error`} className="text-sm text-destructive">
                  {error.message}
                </p>
              )}
            </div>
          );
        })}
      </fieldset>

      {mutation.isError && (
        <p role="alert" className="text-sm text-destructive">
          {mutation.error.message}
        </p>
      )}

      {mutation.isSuccess && (
        <p role="status" className="text-sm">
          Draft created.{" "}
          <Link
            to={`/transfers/${mutation.data.transferId}`}
            className="text-primary underline underline-offset-4"
          >
            Open transfer
          </Link>
        </p>
      )}

      <Button type="submit" disabled={busy}>
        {busy ? "Creating draft…" : "Create draft"}
      </Button>
    </form>
  );
}
