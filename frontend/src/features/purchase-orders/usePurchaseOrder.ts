import { useQuery } from "@tanstack/react-query";
import { getPurchaseOrderById } from "./api";
import { ApiError } from "@/lib/ApiError";

export function usePurchaseOrder(orderId: string) {
  return useQuery({
    queryKey: ["purchase-orders", "detail", orderId],
    queryFn: ({ signal }) => getPurchaseOrderById(orderId, signal),
    enabled: orderId.length > 0,
    staleTime: 0,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status < 500) {
        return false;
      }

      return failureCount < 1;
    },
  });
}
