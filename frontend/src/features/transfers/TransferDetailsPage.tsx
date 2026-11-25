import { Link, useParams } from "react-router";
import { PageHeader } from "@/components/PageHeader";
import { FeedbackPanel } from "@/components/FeedbackPanel";
import { ApiError } from "@/lib/ApiError";
import { useTransfer } from "./useTransfer";
import { transferStatusLabels } from "./transferStatus";
import { TransferLines } from "./TransferLines";
import { usePermissions } from "../auth/usePermissions";
import { EditDraftTransferSection } from "./EditDraftTransferSection";
import { CancelDraftTransferButton } from "./CancelDraftTransferButton";
import { DispatchTransferButton } from "./DispatchTransferButton";
import { ReceiveTransferForm } from "./ReceiveTransferForm";
import { TransferReceiptHistory } from "./TransferReceiptHistory";

export function TransferDetailsPage() {
  const { id } = useParams<{ id: string }>();
  const query = useTransfer(id ?? "");
  const { can } = usePermissions();

  const isNotFound =
    query.error instanceof ApiError && query.error.status === 404;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Transfer details"
        description="Review the route, status, and quantities for this transfer."
      />

      <Link
        to="/transfers"
        className="inline-flex min-h-11 items-center rounded-lg text-sm font-medium text-primary underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-4"
      >
        Back to warehouse transfers
      </Link>

      {!id ? (
        <FeedbackPanel title="Missing transfer identifier" role="alert">
          <p>Open a transfer from the warehouse transfers list.</p>
        </FeedbackPanel>
      ) : query.isPending ? (
        <FeedbackPanel title="Loading transfer" role="status">
          <p>Fetching transfer details.</p>
        </FeedbackPanel>
      ) : query.isError ? (
        <FeedbackPanel
          title={isNotFound ? "Transfer not found" : "Couldn't load transfer"}
          role="alert"
        >
          <p>
            {isNotFound
              ? "Choose another transfer from the list."
              : query.error.message}
          </p>
        </FeedbackPanel>
      ) : (
        <>
          <dl className="grid gap-4 rounded-xl border bg-card p-6 sm:grid-cols-3">
            <div>
              <dt className="text-sm text-muted-foreground">
                Source warehouse
              </dt>
              <dd className="wrap-anywhere font-medium">
                {query.data.sourceWarehouseName}
              </dd>
              <dd className="text-sm text-muted-foreground">
                {query.data.sourceWarehouseCode}
              </dd>
            </div>

            <div>
              <dt className="text-sm text-muted-foreground">
                Destination warehouse
              </dt>
              <dd className="wrap-anywhere font-medium">
                {query.data.destinationWarehouseName}
              </dd>
              <dd className="text-sm text-muted-foreground">
                {query.data.destinationWarehouseCode}
              </dd>
            </div>

            <div>
              <dt className="text-sm text-muted-foreground">Status</dt>
              <dd className="font-medium">
                {transferStatusLabels[query.data.status]}
              </dd>
            </div>
          </dl>

          {query.data.status === 1 && (
            <div className="flex flex-wrap items-start gap-3">
              {can("Transfers.Manage") && (
                <>
                  <EditDraftTransferSection
                    key={query.data.id}
                    transfer={query.data}
                  />

                  <CancelDraftTransferButton
                    key={`cancel-${query.data.id}`}
                    transfer={query.data}
                  />
                </>
              )}

              {can("Transfers.Dispatch") && (
                <DispatchTransferButton
                  key={`dispatch-${query.data.id}`}
                  transfer={query.data}
                />
              )}
            </div>
          )}

          <TransferLines lines={query.data.lines} />

          {can("Transfers.Receive") &&
            (query.data.status === 2 ||
              query.data.status === 3 ||
              query.data.status === 4) && (
              <ReceiveTransferForm
                key={`receive-${query.data.id}`}
                transfer={query.data}
              />
            )}

          <TransferReceiptHistory transfer={query.data} />
        </>
      )}
    </div>
  );
}
