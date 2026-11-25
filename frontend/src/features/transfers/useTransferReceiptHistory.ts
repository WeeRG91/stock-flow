import { useQuery } from "@tanstack/react-query";
import { getTransferReceiptHistory } from "./api";
import { ApiError } from "@/lib/ApiError";

export function useTransferReceiptHistory(transferId: string) {
  return useQuery({
    queryKey: ["transfers", "receipt-history", transferId],
    queryFn: ({ signal }) => getTransferReceiptHistory(transferId, signal),
    enabled: transferId.length > 0,
    staleTime: 0,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status < 500) {
        return false;
      }

      return failureCount < 1;
    },
  });
}
