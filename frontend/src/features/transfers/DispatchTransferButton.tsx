import { useRef } from "react";
import { Button } from "@/components/ui/button";
import type { TransferDetailsDto } from "@/types/api";
import { useDispatchTransfer } from "./useDispatchTransfer";

export function DispatchTransferButton({
  transfer,
}: {
  transfer: TransferDetailsDto;
}) {
  const mutation = useDispatchTransfer(transfer.id);
  const submitting = useRef(false);

  async function dispatch() {
    if (
      submitting.current ||
      mutation.isPending ||
      transfer.lines.length === 0
    ) {
      return;
    }

    const quantities = transfer.lines
      .map((line) => `${line.productSku}: ${line.quantity} units`)
      .join("\n");

    const confirmed = window.confirm(
      `Dispatch the saved transfer from ${transfer.sourceWarehouseCode} ` +
        `to ${transfer.destinationWarehouseCode}?\n\n` +
        `${quantities}\n\n` +
        "This deducts stock from the source warehouse. " +
        "Destination stock increases only when receiving is confirmed.",
    );

    if (!confirmed) return;

    submitting.current = true;

    try {
      await mutation.mutateAsync(transfer.version);
    } catch {
      // The mutation exposes the error for display below.
    } finally {
      submitting.current = false;
    }
  }

  return (
    <div className="space-y-2">
      <Button
        type="button"
        className="min-h-11"
        disabled={mutation.isPending || transfer.lines.length === 0}
        onClick={() => void dispatch()}
      >
        {mutation.isPending ? "Dispatching…" : "Dispatch transfer"}
      </Button>

      {transfer.lines.length === 0 && (
        <p className="text-sm text-muted-foreground">
          Add and save product lines before dispatching.
        </p>
      )}

      {mutation.isError && (
        <p role="alert" className="text-sm text-destructive">
          {mutation.error.message}
        </p>
      )}
    </div>
  );
}