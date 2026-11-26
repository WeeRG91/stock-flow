import { zodResolver } from "@hookform/resolvers/zod";
import { useForm, useWatch } from "react-hook-form";
import { Label } from "@/components/ui/label";
import type {
  CreateStockReservationRequest,
  ProductDto,
  WarehouseDto,
} from "@/types/api";
import {
  createStockReservationSchema,
  type CreateStockReservationFormValues,
} from "./createStockReservationSchema";
import { useReservationAvailability } from "./useReservationAvailability";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useCreateStockReservation } from "./useCreateStockReservation";
import { useRef, useState } from "react";
import { ApiError } from "@/lib/ApiError";
import { Link } from "react-router";

type Props = {
  products: readonly ProductDto[];
  warehouses: readonly WarehouseDto[];
};

const selectClassName =
  "h-11 w-full min-w-0 rounded-lg border border-input bg-card px-3 text-sm " +
  "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring";

export function CreateStockReservationForm({ products, warehouses }: Props) {
  const {
    register,
    control,
    handleSubmit,
    setError,
    reset,
    formState: { errors },
  } = useForm<CreateStockReservationFormValues>({
    resolver: zodResolver(createStockReservationSchema),
    mode: "onBlur",
    defaultValues: {
      productId: "",
      warehouseId: "",
      quantity: 1,
      reference: "",
    },
  });

  const productId = useWatch({ control, name: "productId" });
  const warehouseId = useWatch({ control, name: "warehouseId" });

  const hasSelection = productId.length > 0 && warehouseId.length > 0;
  const availability = useReservationAvailability(productId, warehouseId);
  const balance = availability.data?.items[0];

  const activeProducts = products.filter((product) => product.isActive);
  const activeWarehouses = warehouses.filter((warehouse) => warehouse.isActive);

  const mutation = useCreateStockReservation();

  const [attempt, setAttempt] = useState<CreateStockReservationRequest | null>(
    null,
  );

  const submitting = useRef(false);

  async function sendRequest(request: CreateStockReservationRequest) {
    if (submitting.current) return;

    submitting.current = true;
    setAttempt(request);

    try {
      await mutation.mutateAsync(request);
      setAttempt(null);
      reset();
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        setAttempt(null);
      }
    } finally {
      submitting.current = false;
    }
  }

  function onSubmit(values: CreateStockReservationFormValues) {
    if (submitting.current) return;

    if (!availability.isSuccess || availability.isFetching || !balance) {
      setError("root", {
        message: "Wait for availability to load successfully.",
      });
      return;
    }

    if (
      !activeProducts.some((product) => product.id === values.productId) ||
      !activeWarehouses.some((warehouse) => warehouse.id === values.warehouseId)
    ) {
      setError("root", {
        message: "Select an active product and warehouse.",
      });
      return;
    }

    if (values.quantity > balance.quantityAvailable) {
      setError("quantity", {
        message: "Quantity exceeds the currently available stock.",
      });
      return;
    }

    void sendRequest({
      requestId: crypto.randomUUID(),
      inventoryId: balance.inventoryId,
      quantity: values.quantity,
      reference: values.reference,
    });
  }

  return (
    <form
      aria-labelledby="create-reservation-heading"
      className="space-y-6 rounded-xl border bg-card p-6 shadow-sm"
      noValidate
      onSubmit={(event) => {
        if (attempt) {
          event.preventDefault();
          void sendRequest(attempt);
          return;
        }

        void handleSubmit(onSubmit)(event);
      }}
    >
      <div className="space-y-1">
        <h2 id="create-reservation-heading" className="font-semibold">
          Reserve stock
        </h2>
        <p className="text-sm text-muted-foreground">
          Allocate available stock without changing the physical balance.
        </p>
      </div>

      <fieldset disabled={attempt !== null} className="min-w-0 space-y-6">
        <div className="grid gap-5 sm:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="reservation-product">Product</Label>
            <select
              id="reservation-product"
              className={selectClassName}
              aria-invalid={Boolean(errors.productId)}
              aria-describedby={
                errors.productId ? "reservation-product-error" : undefined
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
              <p
                id="reservation-product-error"
                className="text-sm text-destructive"
              >
                {errors.productId.message}
              </p>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="reservation-warehouse">Warehouse</Label>
            <select
              id="reservation-warehouse"
              className={selectClassName}
              aria-invalid={Boolean(errors.warehouseId)}
              aria-describedby={
                errors.warehouseId ? "reservation-warehouse-error" : undefined
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
                id="reservation-warehouse-error"
                className="text-sm text-destructive"
              >
                {errors.warehouseId.message}
              </p>
            )}
          </div>
        </div>

        <div aria-live="polite" className="text-sm">
          {!hasSelection ? (
            <p className="text-muted-foreground">
              Select a product and warehouse to check availability.
            </p>
          ) : availability.isPending ? (
            <p role="status">Checking availability…</p>
          ) : availability.isError ? (
            <div role="alert" className="space-y-2">
              <p className="text-destructive">{availability.error.message}</p>
              <Button
                type="button"
                variant="outline"
                onClick={() => void availability.refetch()}
              >
                Try again
              </Button>
            </div>
          ) : !balance ? (
            <p className="text-muted-foreground">
              No inventory balance exists for this product and warehouse.
              Receive stock first.
            </p>
          ) : (
            <dl className="grid grid-cols-3 gap-4 rounded-lg bg-muted p-4">
              <div>
                <dt className="text-muted-foreground">On hand</dt>
                <dd className="font-semibold">{balance.quantityOnHand}</dd>
              </div>
              <div>
                <dt className="text-muted-foreground">Reserved</dt>
                <dd className="font-semibold">{balance.quantityReserved}</dd>
              </div>
              <div>
                <dt className="text-muted-foreground">Available</dt>
                <dd className="font-semibold">{balance.quantityAvailable}</dd>
              </div>
            </dl>
          )}
        </div>

        <div className="grid gap-5 sm:grid-cols-[10rem_minmax(0,1fr)]">
          <div className="space-y-2">
            <Label htmlFor="reservation-quantity">Quantity</Label>
            <Input
              id="reservation-quantity"
              type="number"
              min={1}
              max={2_147_483_647}
              step={1}
              className="h-11"
              aria-invalid={Boolean(errors.quantity)}
              aria-describedby={
                errors.quantity ? "reservation-quantity-error" : undefined
              }
              {...register("quantity", { valueAsNumber: true })}
            />
            {errors.quantity && (
              <p
                id="reservation-quantity-error"
                className="text-sm text-destructive"
              >
                {errors.quantity.message}
              </p>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="reservation-reference">Reference</Label>
            <Input
              id="reservation-reference"
              type="text"
              className="h-11"
              placeholder="For example: customer order SO-1042"
              aria-invalid={Boolean(errors.reference)}
              aria-describedby={
                errors.reference ? "reservation-reference-error" : undefined
              }
              {...register("reference")}
            />
            {errors.reference && (
              <p
                id="reservation-reference-error"
                className="text-sm text-destructive"
              >
                {errors.reference.message}
              </p>
            )}
          </div>
        </div>
      </fieldset>

      {errors.root && (
        <p role="alert" className="text-sm text-destructive">
          {errors.root.message}
        </p>
      )}

      <Button
        type="submit"
        className="min-h-11 w-full sm:w-auto"
        disabled={mutation.isPending}
      >
        {mutation.isPending
          ? "Reserving stock…"
          : attempt
            ? "Retry same reservation"
            : "Reserve stock"}
      </Button>

      {mutation.isSuccess && (
        <p role="status" className="text-sm">
          Reservation confirmed.{" "}
          <Link
            to={`/stock-reservations/${mutation.data.reservationId}`}
            className="rounded-sm font-medium underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-4"
          >
            View reservation
          </Link>
        </p>
      )}

      {mutation.isError && (
        <div role="alert" className="space-y-2 text-sm text-destructive">
          <p>
            {mutation.error instanceof ApiError
              ? mutation.error.message
              : "Couldn't confirm the result. Check your connection."}
          </p>
          {attempt && (
            <p>
              Your original request has been retained. Retry the same
              reservation to check its outcome without creating another. You can
              also review the reservation list below.
            </p>
          )}
        </div>
      )}
    </form>
  );
}
