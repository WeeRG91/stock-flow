import type { TransferDetailsDto } from "@/types/api";
import { Button } from "@/components/ui/button";
import { FeedbackPanel } from "@/components/FeedbackPanel";
import { useTransferReceiptHistory } from "./useTransferReceiptHistory";

const receiptDateFormat = new Intl.DateTimeFormat(undefined, {
  dateStyle: "medium",
  timeStyle: "short",
});

export function TransferReceiptHistory({
  transfer,
}: {
  transfer: TransferDetailsDto;
}) {
  const query = useTransferReceiptHistory(transfer.id);
  const transferLines = new Map(
    transfer.lines.map((line) => [line.id, line]),
  );

  return (
    <section
      aria-labelledby="transfer-receipt-history-heading"
      className="space-y-4 rounded-xl border bg-card p-6"
    >
      <h2
        id="transfer-receipt-history-heading"
        className="font-semibold"
      >
        Receipt history
      </h2>

      {query.isPending ? (
        <p role="status" className="text-sm text-muted-foreground">
          Loading receipts…
        </p>
      ) : query.isError ? (
        <FeedbackPanel title="Couldn't load receipts" role="alert">
          <p>{query.error.message}</p>
          <Button
            type="button"
            variant="outline"
            disabled={query.isFetching}
            onClick={() => void query.refetch()}
          >
            Try again
          </Button>
        </FeedbackPanel>
      ) : query.data.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          No receipts have been confirmed.
        </p>
      ) : (
        <ol className="space-y-3">
          {query.data.map((receipt) => (
            <li
              key={receipt.receiptId}
              className="space-y-2 rounded-lg border p-4"
            >
              <p className="font-medium">
                <time dateTime={receipt.confirmedAt}>
                  {receiptDateFormat.format(new Date(receipt.confirmedAt))}
                </time>
              </p>

              <p className="wrap-anywhere text-sm text-muted-foreground">
                Receipt {receipt.receiptId}
              </p>

              <ul className="divide-y">
                {receipt.lines.map((line) => {
                  const product = transferLines.get(line.transferLineId);

                  return (
                    <li
                      key={line.transferLineId}
                      className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1 py-2 text-sm"
                    >
                      <span className="min-w-0 wrap-anywhere">
                        {product
                          ? `${product.productName} (${product.productSku})`
                          : `Transfer line ${line.transferLineId}`}
                      </span>

                      <span className="whitespace-nowrap">
                        {line.receivedQuantity} received
                      </span>
                    </li>
                  );
                })}
              </ul>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}