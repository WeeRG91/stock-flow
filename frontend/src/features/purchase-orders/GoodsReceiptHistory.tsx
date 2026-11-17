import type { PurchaseOrderDetailsDto } from "@/types/api";
import { useGoodsReceiptHistory } from "./useGoodsReceiptHistory";
import { FeedbackPanel } from "@/components/FeedbackPanel";
import { Button } from "@/components/ui/button";

const confirmedAtFormat = new Intl.DateTimeFormat(undefined, {
  dateStyle: "medium",
  timeStyle: "short",
});

export function GoodsReceiptHistory({
  order,
}: {
  order: PurchaseOrderDetailsDto;
}) {
  const query = useGoodsReceiptHistory(order.id);
  const orderLines = new Map(order.lines.map((line) => [line.id, line]));

  return (
    <section
      aria-labelledby="receipt-history-heading"
      className="space-y-4 rounded-xl border bg-card p-6"
    >
      <h2 id="receipt-history-heading" className="font-semibold">
        Receipt history
      </h2>

      {query.isPending ? (
        <p role="status" className="text-sm text-muted-foreground">
          Loading deliveries…
        </p>
      ) : query.isError ? (
        <FeedbackPanel title="Couldn't load deliveries" role="alert">
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
        <p className="text-sm text-muted-foreground">No deliveries yet.</p>
      ) : (
        <ol className="space-y-3">
          {query.data.map((receipt) => (
            <li
              key={receipt.receiptId}
              className="space-y-2 rounded-lg border p-4"
            >
              <p className="font-medium">
                <time dateTime={receipt.confirmedAt}>
                  {confirmedAtFormat.format(new Date(receipt.confirmedAt))}
                </time>
              </p>
              <p className="wrap-anywhere text-sm text-muted-foreground">
                Receipt {receipt.receiptId}
              </p>
              <ul className="divide-y">
                {receipt.lines.map((line) => {
                  const orderLine = orderLines.get(line.purchaseOrderLineId);

                  return (
                    <li
                      key={line.purchaseOrderLineId}
                      className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1 py-2 text-sm"
                    >
                      <span className="min-w-0 wrap-anywhere">
                        {orderLine
                          ? `${orderLine.productName} (${orderLine.productSku})`
                          : `Order line ${line.purchaseOrderLineId}`}
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
