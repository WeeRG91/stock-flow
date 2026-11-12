import { zodResolver } from "@hookform/resolvers/zod";
import { useId, useState } from "react";
import { useFieldArray, useForm } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/ApiError";
import type { ProductDto, PurchaseOrderDetailsDto } from "@/types/api";
import {
  updateDraftPurchaseOrderSchema,
  type UpdateDraftPurchaseOrderFormValues,
} from "./updateDraftPurchaseOrderSchema";
import { useUpdateDraftPurchaseOrder } from "./useUpdateDraftPurchaseOrder";

type EditDraftPurchaseOrderFormProps = {
  order: PurchaseOrderDetailsDto;
  products: readonly ProductDto[];
  onSaved: () => void;
};

export function EditDraftPurchaseOrderForm({
  order,
  products,
  onSaved,
}: EditDraftPurchaseOrderFormProps) {
  const formId = useId();
  const [originalOrder] = useState(order);
  const mutation = useUpdateDraftPurchaseOrder(order.id);

  const {
    control,
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<UpdateDraftPurchaseOrderFormValues>({
    resolver: zodResolver(updateDraftPurchaseOrderSchema),
    defaultValues: {
      lines: originalOrder.lines.map(({ productId, orderedQuantity, unitPrice }) => ({
        productId,
        orderedQuantity,
        unitPrice,
      })),
    },
  });

  const { fields, append, remove } = useFieldArray({ control, name: "lines" });

  const activeProducts = products.filter((product) => product.isActive);
  const retainedProducts = originalOrder.lines.filter(
    (line) => !activeProducts.some((product) => product.id === line.productId),
  );

  function onSubmit(values: UpdateDraftPurchaseOrderFormValues) {
    if (mutation.isPending) return;

    mutation.mutate(
      { expectedVersion: originalOrder.version, lines: values.lines },
      { onSuccess: onSaved },
    );
  }

  return (
    <form
      aria-labelledby={`${formId}-heading`}
      className="space-y-4 rounded-xl border bg-card p-6 shadow-sm"
      noValidate
      onSubmit={handleSubmit(onSubmit)}
    >
      <h2 id={`${formId}-heading`} className="font-semibold">Edit draft lines</h2>

      {fields.map((field, index) => {
        const rowId = `${formId}-${field.id}`;
        const rowErrors = errors.lines?.[index];

        return (
          <fieldset key={field.id} disabled={mutation.isPending} className="space-y-3 rounded-lg border p-4">
            <legend className="px-1 text-sm font-medium">Line {index + 1}</legend>
            <div className="grid gap-4 sm:grid-cols-3">
              <div className="space-y-2">
                <Label htmlFor={`${rowId}-product`}>Product</Label>
                <select
                  id={`${rowId}-product`}
                  className="h-11 w-full rounded-lg border border-input bg-card px-3 text-sm"
                  aria-invalid={!!rowErrors?.productId}
                  aria-describedby={rowErrors?.productId ? `${rowId}-product-error` : undefined}
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
                  <p id={`${rowId}-product-error`} className="text-sm text-destructive">
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
                  step={1}
                  aria-invalid={!!rowErrors?.orderedQuantity}
                  aria-describedby={rowErrors?.orderedQuantity ? `${rowId}-quantity-error` : undefined}
                  {...register(`lines.${index}.orderedQuantity`, { valueAsNumber: true })}
                />
                {rowErrors?.orderedQuantity && (
                  <p id={`${rowId}-quantity-error`} className="text-sm text-destructive">
                    {rowErrors.orderedQuantity.message}
                  </p>
                )}
              </div>

              <div className="space-y-2">
                <Label htmlFor={`${rowId}-price`}>Unit price ({order.currency})</Label>
                <Input
                  id={`${rowId}-price`}
                  type="number"
                  min={0}
                  step="0.01"
                  aria-invalid={!!rowErrors?.unitPrice}
                  aria-describedby={rowErrors?.unitPrice ? `${rowId}-price-error` : undefined}
                  {...register(`lines.${index}.unitPrice`, { valueAsNumber: true })}
                />
                {rowErrors?.unitPrice && (
                  <p id={`${rowId}-price-error`} className="text-sm text-destructive">
                    {rowErrors.unitPrice.message}
                  </p>
                )}
              </div>
            </div>
            <Button type="button" variant="outline" onClick={() => remove(index)}>
              Remove line {index + 1}
            </Button>
          </fieldset>
        );
      })}

      {mutation.isError && (
        <p role="alert" className="text-sm text-destructive">
          {mutation.error instanceof ApiError && mutation.error.status === 409
            ? "The draft could not be saved. If the order changed, reopen the editor to review the latest version."
            : mutation.error.message}
        </p>
      )}

      <div className="flex flex-wrap gap-3">
        <Button
          type="button"
          variant="outline"
          disabled={mutation.isPending}
          onClick={() => append({ productId: "", orderedQuantity: 1, unitPrice: 0 })}
        >
          Add line
        </Button>
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending ? "Saving…" : "Save lines"}
        </Button>
      </div>
    </form>
  );
}