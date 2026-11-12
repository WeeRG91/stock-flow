import { useQuery } from "@tanstack/react-query";
import { getPurchaseOrders, type GetPurchaseOrdersParameters } from "./api";
import { ApiError } from "@/lib/ApiError";

export function usePurchaseOrders(parameters: GetPurchaseOrdersParameters) {
  return useQuery({
    queryKey: ["purchase-orders", "list", parameters],
    queryFn: ({ signal }) => getPurchaseOrders(parameters, signal),
    staleTime: 0,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status < 500) {
        return false;
      }

      return failureCount < 1;
    },
  });
}
