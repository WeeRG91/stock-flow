import type { TransferLineDto } from "@/types/api";
import { FeedbackPanel } from "@/components/FeedbackPanel";

export function TransferLines({ lines }: { lines: TransferLineDto[] }) {
  if (lines.length === 0) {
    return (
      <FeedbackPanel title="No product lines">
        <p>Add products to this draft before dispatching it.</p>
      </FeedbackPanel>
    );
  }

  return (
    <section aria-labelledby="transfer-lines-heading" className="space-y-3">
      <h2 id="transfer-lines-heading" className="text-lg font-semibold">
        Product quantities
      </h2>

      <div
        role="region"
        aria-label="Transfer product quantities"
        tabIndex={0}
        className="overflow-x-auto rounded-xl border bg-card focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
      >
        <table className="w-full text-sm">
          <caption className="sr-only">
            Planned, dispatched, received, and in-transit quantities by product
          </caption>

          <thead className="border-b bg-muted/50">
            <tr>
              <th scope="col" className="px-4 py-3 text-left">Product</th>
              <th scope="col" className="px-4 py-3 text-right">Planned</th>
              <th scope="col" className="px-4 py-3 text-right">Dispatched</th>
              <th scope="col" className="px-4 py-3 text-right">Received</th>
              <th scope="col" className="whitespace-nowrap px-4 py-3 text-right">
                In transit
              </th>
            </tr>
          </thead>

          <tbody className="divide-y">
            {lines.map((line) => (
              <tr key={line.id}>
                <th scope="row" className="px-4 py-3 text-left font-medium">
                  <p>{line.productName}</p>
                  <p className="text-xs font-normal text-muted-foreground">
                    {line.productSku}
                  </p>
                </th>
                <td className="px-4 py-3 text-right tabular-nums">{line.quantity}</td>
                <td className="px-4 py-3 text-right tabular-nums">{line.dispatchedQuantity}</td>
                <td className="px-4 py-3 text-right tabular-nums">{line.receivedQuantity}</td>
                <td className="px-4 py-3 text-right tabular-nums">{line.inTransitQuantity}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}