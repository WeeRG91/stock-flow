import { useId, useRef, useState, type FormEvent } from "react";
import { Button } from "@/components/ui/button";
import type { ReceiveTransferRequest, TransferDetailsDto } from "@/types/api";
import { useReceiveTransfer } from "./useReceiveTransfer";
import { validateTransferReceipt } from "./validateTransferReceipt";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";

export function ReceiveTransferForm({
  transfer,
}: {
  transfer: TransferDetailsDto;
}) {
  const formId = useId();
  const submitting = useRef(false);
  const [quantities, setQuantities] = useState<Record<string, string>>({});
  const [attempt, setAttempt] = useState<ReceiveTransferRequest | null>(null);
  const [validationError, setValidationError] = useState<{
    message: string;
    lineId?: string;
  } | null>(null);

  const mutation = useReceiveTransfer(transfer.id);
  const openLines = transfer.lines.filter((line) => line.inTransitQuantity > 0);

  function send(request: ReceiveTransferRequest) {
    if (submitting.current) return;

    submitting.current = true;

    mutation.mutate(request, {
      onSuccess: () => {
        setAttempt(null);
        setQuantities({});
      },
      onSettled: () => {
        submitting.current = false;
      },
    });
  }

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (submitting.current || mutation.isPending || attempt) return;

    setValidationError(null);
    mutation.reset();

    const result = validateTransferReceipt(transfer.lines, quantities);

    if (!result.valid) {
      setValidationError(result);

      if (result.lineId) {
        document.getElementById(`${formId}-${result.lineId}`)?.focus();
      }

      return;
    }

    const request: ReceiveTransferRequest = {
      receiptId: crypto.randomUUID(),
      expectedVersion: transfer.version,
      lines: result.lines,
    };

    setAttempt(request);
    send(request);
  }

  if (openLines.length === 0 && attempt === null) {
    return mutation.isSuccess ? (
      <p role="status">Transfer receipt confirmed.</p>
    ) : null;
  }

  return (
    <form
      aria-labelledby={`${formId}-heading`}
      className="space-y-4 rounded-xl border bg-card p-6 shadow-sm"
      onSubmit={onSubmit}
      noValidate
    >
      <h2 id={`${formId}-heading`} className="font-semibold">
        Receive transferred stock
      </h2>

      <p className="text-sm text-muted-foreground">
        Enter quantities physically received at{" "}
        {transfer.destinationWarehouseName}. Leave other products blank.
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
                max={line.inTransitQuantity}
                step={1}
                className="h-11 sm:max-w-48"
                value={quantities[line.id] ?? ""}
                aria-invalid={!!lineError}
                aria-describedby={
                  lineError
                    ? `${inputId}-remaining ${inputId}-error`
                    : `${inputId}-remaining`
                }
                onChange={(event) => {
                  const value = event.currentTarget.value;

                  if (lineError) setValidationError(null);

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
                Dispatched: {line.dispatchedQuantity} · Received:{" "}
                {line.receivedQuantity} · In transit: {line.inTransitQuantity}
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
        <div role="alert" className="space-y-2 text-sm text-destructive">
          <p>{mutation.error.message}</p>
          <p>
            Retry the same confirmation to check whether it was recorded. If a
            conflict persists, review receipt history before starting another
            confirmation.
          </p>
        </div>
      )}

      {mutation.isSuccess && <p role="status">Transfer receipt confirmed.</p>}

      {attempt && mutation.isError ? (
        <Button
          type="button"
          disabled={mutation.isPending}
          onClick={() => send(attempt)}
        >
          Retry confirmation
        </Button>
      ) : (
        <Button
          type="submit"
          disabled={attempt !== null || openLines.length === 0}
        >
          {mutation.isPending ? "Confirming…" : "Confirm receipt"}
        </Button>
      )}
    </form>
  );
}
