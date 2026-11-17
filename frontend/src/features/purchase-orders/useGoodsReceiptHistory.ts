import { useQuery } from "@tanstack/react-query";
import { getGoodsReceiptHistory } from "./api";
import { ApiError } from "@/lib/ApiError";

export function useGoodsReceiptHistory(orderId: string) {
  return useQuery({
    queryKey: ["purchase-orders", "receipt-history", orderId],
    queryFn: ({ signal }) => getGoodsReceiptHistory(orderId, signal),
    enabled: orderId.length > 0,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status < 500) {
        return false;
      }

      return failureCount < 1;
    },
  });
}
