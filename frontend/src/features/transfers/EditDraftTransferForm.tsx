import { zodResolver } from "@hookform/resolvers/zod";
import { useId, useState } from "react";
import { useFieldArray, useForm } from "react-hook-form";
import { Button } from "@/components/ui/button";
import type { ProductDto, TransferDetailsDto } from "@/types/api";
import {
  updateDraftTransferSchema,
  type UpdateDraftTransferFormValues,
} from "./updateDraftTransferSchema";
import { useUpdateDraftTransfer } from "./useUpdateDraftTransfer";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/ApiError";

export function EditDraftTransferForm({
  transfer,
  products,
  onSaved,
  onCancel,
}: {
  transfer: TransferDetailsDto;
  products: readonly ProductDto[];
  onSaved: () => void;
  onCancel: () => void;
}) {
  const formId = useId();
  const [originalTransfer] = useState(transfer);
  const mutation = useUpdateDraftTransfer(originalTransfer.id);

  const {
    control,
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<UpdateDraftTransferFormValues>({
    resolver: zodResolver(updateDraftTransferSchema),
    defaultValues: {
      lines: originalTransfer.lines.map(({ productId, quantity }) => ({
        productId,
        quantity,
      })),
    },
  });

  const { fields, append, remove } = useFieldArray({
    control,
    name: "lines",
  });

  const activeProducts = products.filter((product) => product.isActive);

  const retainedProducts = originalTransfer.lines.filter(
    (line) => !activeProducts.some((product) => product.id === line.productId),
  );

  const hasConflict = mutation.error instanceof ApiError && mutation.error.status === 409;
  const busy = mutation.isPending || isSubmitting;
  const isStale = transfer.version !== originalTransfer.version;

  const blocked = busy || isStale || hasConflict;

  async function onSubmit(values: UpdateDraftTransferFormValues) {
    if (blocked) return;

    try {
      await mutation.mutateAsync({
        expectedVersion: originalTransfer.version,
        lines: values.lines,
      });

      onSaved();
    } catch {
      // The mutation exposes the error for display below.
    }
  }

  return (
    <form
      aria-labelledby={`${formId}-heading`}
      className="w-full basis-full space-y-4 rounded-xl border bg-card p-6 shadow-sm"
      onSubmit={handleSubmit(onSubmit)}
      noValidate
    >
      <h2 id={`${formId}-heading`} className="font-semibold">
        Edit transfer products
      </h2>

      <fieldset disabled={blocked} className="space-y-4">
        <legend className="mb-3 font-medium">Product lines</legend>

        {fields.length === 0 && (
          <p className="text-sm text-muted-foreground">
            This draft has no products. Add a product before dispatching.
          </p>
        )}

        {fields.map((field, index) => {
          const rowId = `${formId}-${field.id}`;
          const rowErrors = errors.lines?.[index];

          return (
            <div
              key={field.id}
              className="grid gap-4 rounded-lg border p-4 sm:grid-cols-[minmax(0,1fr)_8rem_auto] sm:items-start"
            >
              <div className="space-y-2">
                <Label htmlFor={`${rowId}-product`}>Product {index + 1}</Label>

                <select
                  id={`${rowId}-product`}
                  className="h-11 w-full rounded-lg border border-input bg-card px-3 text-sm aria-invalid:border-destructive focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                  aria-invalid={!!rowErrors?.productId}
                  aria-describedby={
                    rowErrors?.productId ? `${rowId}-product-error` : undefined
                  }
                  {...register(`lines.${index}.productId`)}
                >
                  <option value="">Select a product</option>

                  {activeProducts.map((product) => (
                    <option key={product.id} value={product.id}>
                      {product.sku} — {product.name}
                    </option>
                  ))}

                  {retainedProducts.map((line) => (
                    <option key={line.productId} value={line.productId}>
                      {line.productSku} — {line.productName}
                    </option>
                  ))}
                </select>

                {rowErrors?.productId && (
                  <p
                    id={`${rowId}-product-error`}
                    className="text-sm text-destructive"
                  >
                    {rowErrors.productId.message}
                  </p>
                )}
              </div>

              <div className="space-y-2">
                <Label htmlFor={`${rowId}-quantity`}>Quantity</Label>

                <Input
                  id={`${rowId}-quantity`}
                  type="number"
                  min={1}
                  max={2_147_483_647}
                  step={1}
                  className="h-11"
                  aria-invalid={!!rowErrors?.quantity}
                  aria-describedby={
                    rowErrors?.quantity ? `${rowId}-quantity-error` : undefined
                  }
                  {...register(`lines.${index}.quantity`, {
                    valueAsNumber: true,
                  })}
                />

                {rowErrors?.quantity && (
                  <p
                    id={`${rowId}-quantity-error`}
                    className="text-sm text-destructive"
                  >
                    {rowErrors.quantity.message}
                  </p>
                )}
              </div>

              <Button
                type="button"
                variant="outline"
                className="min-h-11 sm:mt-7"
                aria-label={`Remove product line ${index + 1}`}
                onClick={() => remove(index)}
              >
                Remove
              </Button>
            </div>
          );
        })}

        <Button
          type="button"
          variant="outline"
          disabled={activeProducts.length === 0}
          onClick={() => append({ productId: "", quantity: 1 })}
        >
          Add product
        </Button>
      </fieldset>

      {(isStale || hasConflict) && (
        <p role="alert" className="text-sm text-destructive">
          This transfer changed while you were editing. Close the editor and
          reopen it to use the latest version.
        </p>
      )}

      {mutation.isError && (
        <p role="alert" className="text-sm text-destructive">
          {mutation.error.message}
        </p>
      )}

      <div className="flex flex-wrap gap-2">
        <Button type="submit" disabled={blocked}>
          {busy ? "Saving…" : "Save product lines"}
        </Button>

        <Button
          type="button"
          variant="outline"
          disabled={busy}
          onClick={onCancel}
        >
          Close editor
        </Button>
      </div>
    </form>
  );
}
