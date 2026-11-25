import { useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { createTransferConnection } from "./transferConnection";

export function useTransferRealtime(userId: string) {
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!userId) return;

    const connection = createTransferConnection();
    let disposed = false;
    let retryTimer: ReturnType<typeof setTimeout> | undefined;

    function refreshChangedTransfer(notification: { transferId: string }) {
      if (disposed) return;

      void Promise.all([
        queryClient.invalidateQueries({
          queryKey: ["transfers", "list"],
        }),
        queryClient.invalidateQueries({
          queryKey: ["transfers", "detail", notification.transferId],
        }),
        queryClient.invalidateQueries({
          queryKey: ["transfers", "receipt-history", notification.transferId],
        }),
      ]).catch((error: unknown) => {
        console.warn("Couldn't refresh transfer data.", error);
      });
    }

    function refreshAfterConnect() {
      if (disposed) return;

      void Promise.all([
        queryClient.invalidateQueries({
          queryKey: ["transfers"],
        }),
        queryClient.invalidateQueries({
          queryKey: ["inventory"],
        }),
      ]).catch((error: unknown) => {
        console.warn("Couldn't refresh data after connecting.", error);
      });
    }

    function scheduleStart(delay: number) {
      if (disposed || retryTimer !== undefined) return;

      retryTimer = setTimeout(() => {
        retryTimer = undefined;
        void start();
      }, delay);
    }

    async function start() {
      if (disposed) return;

      try {
        await connection.start();
        refreshAfterConnect();
      } catch {
        scheduleStart(5_000);
      }
    }

    connection.on("TransferChanged", refreshChangedTransfer);
    connection.onreconnected(refreshAfterConnect);
    connection.onclose(() => scheduleStart(5_000));

    scheduleStart(0);

    return () => {
      disposed = true;
      clearTimeout(retryTimer);
      connection.off("TransferChanged", refreshChangedTransfer);

      void connection.stop().catch((error: unknown) => {
        console.warn("Couldn't stop the transfer connection cleanly.", error);
      });
    };
  }, [queryClient, userId]);
}
