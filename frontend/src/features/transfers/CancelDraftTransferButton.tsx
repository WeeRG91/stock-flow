import { useRef } from "react";
import { Button } from "@/components/ui/button";
import type { TransferDetailsDto } from "@/types/api";
import { useCancelDraftTransfer } from "./useCancelDraftTransfer";

export function CancelDraftTransferButton({
  transfer,
}: {
  transfer: TransferDetailsDto;
}) {
  const mutation = useCancelDraftTransfer(transfer.id);
  const submitting = useRef(false);

  async function cancel() {
    if (submitting.current || mutation.isPending) return;

    const confirmed = window.confirm(
      `Cancel this draft transfer from ${transfer.sourceWarehouseCode} ` +
        `to ${transfer.destinationWarehouseCode}?\n\n` +
        "The transfer will remain in history. No stock will be moved.",
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
        variant="destructive"
        className="min-h-11"
        disabled={mutation.isPending}
        onClick={() => void cancel()}
      >
        {mutation.isPending ? "Cancelling…" : "Cancel draft"}
      </Button>

      {mutation.isError && (
        <p role="alert" className="text-sm text-destructive">
          {mutation.error.message}
        </p>
      )}
    </div>
  );
}