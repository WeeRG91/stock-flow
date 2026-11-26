import { useRef } from "react";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/ApiError";
import type { StockReservationDto } from "@/types/api";
import { useReleaseStockReservation } from "./useReleaseStockReservation";
import { useFulfillStockReservation } from "./useFulfillStockReservation";

type Props = {
  reservation: StockReservationDto;
  refreshing: boolean;
  canRelease: boolean;
  canFulfill: boolean;
  onRefresh: () => void;
};

export function StockReservationActions({
  reservation,
  refreshing,
  canRelease,
  canFulfill,
  onRefresh,
}: Props) {
  const releaseMutation = useReleaseStockReservation();
  const fulfillMutation = useFulfillStockReservation();
  const submitting = useRef(false);

  const pending = releaseMutation.isPending || fulfillMutation.isPending;

  const error = releaseMutation.error ?? fulfillMutation.error;

  async function execute(action: "release" | "fulfill") {
    if (
      submitting.current ||
      pending ||
      refreshing ||
      reservation.status !== 1 ||
      (action === "release" && !canRelease) ||
      (action === "fulfill" && !canFulfill)
    ) {
      return;
    }

    const message =
      action === "release"
        ? `Release ${reservation.quantity} reserved units? Physical stock will remain unchanged.`
        : `Fulfill this reservation? ${reservation.quantity} units will be physically removed from stock.`;

    if (!window.confirm(message)) return;

    submitting.current = true;
    releaseMutation.reset();
    fulfillMutation.reset();

    try {
      const variables = {
        reservationId: reservation.reservationId,
        expectedVersion: reservation.version,
      };

      if (action === "release") {
        await releaseMutation.mutateAsync(variables);
      } else {
        await fulfillMutation.mutateAsync(variables);
      }
    } catch {
      // The mutation error is displayed below.
    } finally {
      submitting.current = false;
    }
  }

  if (
    reservation.status !== 1 &&
    !pending &&
    !error &&
    !releaseMutation.isSuccess &&
    !fulfillMutation.isSuccess
  ) {
    return null;
  }

  return (
    <div className="space-y-3">
      {reservation.status === 1 && (
        <div className="flex flex-wrap gap-3 border-t pt-6">
          {canRelease && (
            <Button
              type="button"
              variant="outline"
              className="min-h-11"
              disabled={pending || refreshing}
              onClick={() => void execute("release")}
            >
              {releaseMutation.isPending ? "Releasing…" : "Release reservation"}
            </Button>
          )}

          {canFulfill && (
            <Button
              type="button"
              className="min-h-11"
              disabled={pending || refreshing}
              onClick={() => void execute("fulfill")}
            >
              {fulfillMutation.isPending
                ? "Fulfilling…"
                : "Fulfill reservation"}
            </Button>
          )}
        </div>
      )}

      {releaseMutation.isSuccess && (
        <p role="status" className="text-sm">
          Reservation released successfully.
        </p>
      )}

      {fulfillMutation.isSuccess && (
        <p role="status" className="text-sm">
          Reservation fulfilled successfully.
        </p>
      )}

      {error && (
        <div role="alert" className="space-y-2 text-sm">
          <p className="text-destructive">
            {error instanceof ApiError
              ? error.message
              : "Couldn't confirm the operation."}
          </p>
          <p className="text-muted-foreground">
            Refresh the reservation to review its current status before
            attempting another action.
          </p>
          <Button
            type="button"
            variant="outline"
            disabled={refreshing || pending}
            onClick={onRefresh}
          >
            Refresh reservation
          </Button>
        </div>
      )}
    </div>
  );
}
