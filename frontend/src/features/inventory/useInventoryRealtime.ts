import { useEffect, useState } from "react";
import { createInventoryConnection } from "./inventoryConnection";
import { useQueryClient } from "@tanstack/react-query";

type ConnectionStatus =
  | "connecting"
  | "connected"
  | "reconnecting"
  | "disconnected";

export function useInventoryRealtime() {
  const queryClient = useQueryClient();

  const [status, setStatus] = useState<ConnectionStatus>("connecting");

  useEffect(() => {
    const connection = createInventoryConnection();
    let disposed = false;
    let retryTimer: ReturnType<typeof setTimeout> | undefined;

    function refreshInventory() {
      if (disposed) return;

      void queryClient
        .invalidateQueries({ queryKey: ["inventory"] })
        .catch((error: unknown) => {
          console.warn(
            "Couldn't refresh inventory after a notification.",
            error,
          );
        });
    }

    connection.on("InventoryChanged", refreshInventory);

    function scheduleStart() {
      if (disposed || retryTimer !== undefined) return;

      retryTimer = setTimeout(() => {
        retryTimer = undefined;
        if (disposed) return;

        setStatus("connecting");
        void start();
      }, 5_000);
    }

    async function start() {
      if (disposed) return;

      try {
        await connection.start();

        if (!disposed) {
          setStatus("connected");
          refreshInventory();
        }
      } catch {
        if (disposed) return;

        setStatus("disconnected");
        scheduleStart();
      }
    }

    connection.onreconnecting(() => {
      if (!disposed) {
        setStatus("reconnecting");
      }
    });

    connection.onreconnected(() => {
      if (!disposed) {
        setStatus("connected");
        refreshInventory();
      }
    });

    connection.onclose(() => {
      if (disposed) return;

      setStatus("disconnected");
      scheduleStart();
    });

    void start();

    return () => {
      disposed = true;
      clearTimeout(retryTimer);
      connection.off("InventoryChanged", refreshInventory);

      void connection.stop().catch((error: unknown) => {
        console.warn("Couldn't stop the inventory connection cleanly.", error);
      });
    };
  }, [queryClient]);

  return status;
}
