import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";

export function usePurchasingRealtime(userId: string) {
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!userId) return;

    const connection = new HubConnectionBuilder()
      .withUrl("/hubs/purchasing")
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    let disposed = false;
    let retryTimer: ReturnType<typeof setTimeout> | undefined;

    function refreshOrders() {
      if (disposed) return;
      void queryClient.invalidateQueries({ queryKey: ["purchase-orders"] });
    }

    function refreshInbox() {
      if (disposed) return;
      void queryClient.invalidateQueries({
        queryKey: ["purchasing-notifications", userId],
      });
    }

    function refreshAfterConnect() {
      refreshOrders();
      refreshInbox();
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

    connection.on("PurchasingChanged", refreshOrders);
    connection.on("PurchasingNotificationsChanged", refreshInbox);
    connection.onreconnected(refreshAfterConnect);
    connection.onclose(() => scheduleStart(5_000));

    scheduleStart(0);

    return () => {
      disposed = true;
      clearTimeout(retryTimer);
      connection.off("PurchasingChanged", refreshOrders);
      connection.off("PurchasingNotificationsChanged", refreshInbox);

      void connection.stop().catch((error: unknown) => {
        console.warn("Couldn't stop the purchasing connection cleanly.", error);
      });
    };
  }, [queryClient, userId]);
}
