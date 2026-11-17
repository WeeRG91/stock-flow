import { useId, useState, type FormEvent } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/ApiError";
import type {
  ConfirmGoodsReceiptRequest,
  PurchaseOrderDetailsDto,
} from "@/types/api";
import { useConfirmGoodsReceipt } from "./useConfirmGoodsReceipt";

export function ConfirmGoodsReceiptForm({
  order,
}: {
  order: PurchaseOrderDetailsDto;
}) {
  const formId = useId();
  const [quantities, setQuantities] = useState<Record<string, string>>({});
  const [attempt, setAttempt] = useState<ConfirmGoodsReceiptRequest | null>(
    null,
  );
  const [validationError, setValidationError] = useState<{
    message: string;
    lineId?: string;
  } | null>(null);
  const mutation = useConfirmGoodsReceipt(order.id);
  const openLines = order.lines.filter((line) => line.remainingQuantity > 0);

  function send(request: ConfirmGoodsReceiptRequest) {
    mutation.mutate(request, {
      onSuccess: () => {
        setAttempt(null);
        setQuantities({});
      },
    });
  }

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (mutation.isPending || attempt) return;

    setValidationError(null);
    mutation.reset();

    const lines: ConfirmGoodsReceiptRequest["lines"] = [];

    for (const line of openLines) {
      const entered = quantities[line.id]?.trim();
      if (!entered) continue;

      const quantity = Number(entered);
      if (
        !Number.isInteger(quantity) ||
        quantity < 1 ||
        quantity > line.remainingQuantity
      ) {
        setValidationError({
          lineId: line.id,
          message: `Enter a whole number from 1 to ${line.remainingQuantity} for ${line.productName}.`,
        });
        document.getElementById(`${formId}-${line.id}`)?.focus();
        return;
      }

      lines.push({ purchaseOrderLineId: line.id, receivedQuantity: quantity });
    }

    if (lines.length === 0) {
      setValidationError({
        message: "Enter a quantity for at least one order line.",
      });
      return;
    }

    const request = {
      receiptId: crypto.randomUUID(),
      expectedVersion: order.version,
      lines,
    };

    setAttempt(request);
    send(request);
  }

  if (openLines.length === 0 && attempt === null) {
    return mutation.isSuccess ? <p role="status">Delivery confirmed.</p> : null;
  }

  return (
    <form
      aria-labelledby={`${formId}-heading`}
      className="space-y-4 rounded-xl border bg-card p-6 shadow-sm"
      onSubmit={onSubmit}
      noValidate
    >
      <h2 id={`${formId}-heading`} className="font-semibold">
        Receive delivery
      </h2>
      <p className="text-sm text-muted-foreground">
        Enter the units that arrived. Leave other lines blank.
      </p>

      <fieldset disabled={attempt !== null} className="space-y-4">
        {openLines.map((line) => {
          const inputId = `${formId}-${line.id}`;
          const lineError =
            validationError?.lineId === line.id
              ? validationError.message
              : undefined;

          return (
            <div key={line.id} className="space-y-2">
              <Label htmlFor={inputId}>
                {line.productName} ({line.productSku})
              </Label>
              <Input
                id={inputId}
                type="number"
                min={1}
                max={line.remainingQuantity}
                step={1}
                aria-invalid={lineError ? true : undefined}
                aria-describedby={
                  lineError
                    ? `${inputId}-remaining ${inputId}-error`
                    : `${inputId}-remaining`
                }
                value={quantities[line.id] ?? ""}
                onChange={(event) => {
                  if (lineError) setValidationError(null);
                  const value = event.target.value;
                  setQuantities((current) => ({
                    ...current,
                    [line.id]: value,
                  }));
                }}
              />
              <p
                id={`${inputId}-remaining`}
                className="text-sm text-muted-foreground"
              >
                {line.remainingQuantity} remaining
              </p>
              {lineError && (
                <p
                  id={`${inputId}-error`}
                  role="alert"
                  className="text-sm text-destructive"
                >
                  {lineError}
                </p>
              )}
            </div>
          );
        })}
      </fieldset>

      {validationError && !validationError.lineId && (
        <p role="alert" className="text-sm text-destructive">
          {validationError.message}
        </p>
      )}

      {mutation.isError && (
        <p role="alert" className="text-sm text-destructive">
          {mutation.error instanceof ApiError && mutation.error.status === 409
            ? "Retry confirmation to check whether this delivery was already recorded. If the conflict persists, reload the order and review the received quantities before starting another delivery."
            : mutation.error.message}
        </p>
      )}

      {mutation.isSuccess && <p role="status">Delivery confirmed.</p>}

      {attempt && mutation.isError ? (
        <Button type="button" onClick={() => send(attempt)}>
          Retry confirmation
        </Button>
      ) : (
        <Button
          type="submit"
          disabled={attempt !== null || openLines.length === 0}
        >
          {mutation.isPending ? "Confirming…" : "Confirm delivery"}
        </Button>
      )}
    </form>
  );
}
