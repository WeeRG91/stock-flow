import { Label } from "@/components/ui/label";
import type { ProductDto, WarehouseDto } from "@/types/api";
import { useForm } from "react-hook-form";
import {
  receiveStockSchema,
  type ReceiveStockFormValues,
} from "./receiveStockSchema";
import { zodResolver } from "@hookform/resolvers/zod";
import { Input } from "@/components/ui/input";
import { useReceiveStock } from "./useReceiveStock";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/ApiError";

interface ReceiveStockFormProps {
  products: readonly ProductDto[];
  warehouses: readonly WarehouseDto[];
}

const selectClassName =
  "h-10 w-full rounded-lg border border-input bg-background px-3 text-sm " +
  "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring";

export function ReceiveStockForm({
  products,
  warehouses,
}: ReceiveStockFormProps) {
  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors },
  } = useForm<ReceiveStockFormValues>({
    resolver: zodResolver(receiveStockSchema),
    mode: "onBlur",
    defaultValues: {
      productId: "",
      warehouseId: "",
      quantity: 1,
    },
  });

  const mutation = useReceiveStock();

  function onSubmit(values: ReceiveStockFormValues) {
    if (mutation.isPending) return;

    mutation.mutate(values, {
      onSuccess: () => reset(),
      onError: (error) => {
        if (!(error instanceof ApiError) || error.status !== 400) {
          return;
        }

        const serverErrors = error.problem?.errors;
        if (!serverErrors) return;

        const fields = [
          ["productId", "ProductId"],
          ["warehouseId", "WarehouseId"],
          ["quantity", "Quantity"],
        ] as const;

        for (const [field, serverField] of fields) {
          const messages = serverErrors[field] ?? serverErrors[serverField];

          if (messages?.length) {
            setError(field, {
              type: "server",
              message: messages.join(" "),
            });
          }
        }
      },
    });
  }

  const activeProducts = products.filter((product) => product.isActive);
  const activeWarehouses = warehouses.filter((warehouse) => warehouse.isActive);

  return (
    <form
      aria-labelledby="receive-stock-heading"
      className="space-y-4 rounded-xl border bg-card p-6"
      noValidate
      onSubmit={handleSubmit(onSubmit)}
    >
      <h2 id="receive-stock-heading" className="text-lg font-semibold">
        Receive stock
      </h2>

      <fieldset
        disabled={mutation.isPending}
        className="grid min-w-0 gap-4 sm:grid-cols-2"
      >
        <div className="space-y-2">
          <Label htmlFor="receive-product">Product</Label>
          <select
            id="receive-product"
            className={selectClassName}
            aria-invalid={Boolean(errors.productId)}
            aria-describedby={
              errors.productId ? "receive-product-error" : undefined
            }
            {...register("productId")}
          >
            <option value="">Select a product</option>
            {activeProducts.map((product) => (
              <option key={product.id} value={product.id}>
                {product.sku} — {product.name}
              </option>
            ))}
          </select>

          {errors.productId && (
            <p id="receive-product-error" className="text-sm text-destructive">
              {errors.productId.message}
            </p>
          )}
        </div>

        <div className="space-y-2">
          <Label htmlFor="receive-warehouse">Warehouse</Label>
          <select
            id="receive-warehouse"
            className={selectClassName}
            aria-invalid={Boolean(errors.warehouseId)}
            aria-describedby={
              errors.warehouseId ? "receive-warehouse-error" : undefined
            }
            {...register("warehouseId")}
          >
            <option value="">Select a warehouse</option>
            {activeWarehouses.map((warehouse) => (
              <option key={warehouse.id} value={warehouse.id}>
                {warehouse.code} — {warehouse.name}
              </option>
            ))}
          </select>

          {errors.warehouseId && (
            <p
              id="receive-warehouse-error"
              className="text-sm text-destructive"
            >
              {errors.warehouseId.message}
            </p>
          )}
        </div>

        <div className="space-y-2">
          <Label htmlFor="receive-quantity">Quantity</Label>
          <Input
            id="receive-quantity"
            type="number"
            min={1}
            max={2_147_483_647}
            step={1}
            aria-invalid={Boolean(errors.quantity)}
            aria-describedby={
              errors.quantity ? "receive-quantity-error" : undefined
            }
            {...register("quantity", { valueAsNumber: true })}
          />
          {errors.quantity && (
            <p id="receive-quantity-error" className="text-sm text-destructive">
              {errors.quantity.message}
            </p>
          )}
        </div>
      </fieldset>

      <Button
        type="submit"
        disabled={
          mutation.isPending ||
          activeProducts.length === 0 ||
          activeWarehouses.length === 0
        }
      >
        {mutation.isPending ? "Receiving stock…" : "Receive stock"}
      </Button>

      {mutation.isSuccess && (
        <p role="status" className="text-sm">
          Stock received successfully. Quantity on hand:{" "}
          {mutation.data.quantityOnHand}.
        </p>
      )}

      {mutation.isError && (
        <div role="alert" className="space-y-1 text-sm text-destructive">
          <p>
            {mutation.error instanceof ApiError
              ? mutation.error.message
              : "Couldn't confirm the receipt. Check your connection and inventory before submitting again."}
          </p>

          {mutation.error instanceof ApiError &&
            mutation.error.status === 409 && (
              <p>
                Review the latest inventory before submitting again. If
                inventory couldn't refresh, use its Try again button.
              </p>
            )}
        </div>
      )}
    </form>
  );
}
