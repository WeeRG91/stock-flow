import { useQuery } from "@tanstack/react-query";
import { ApiError } from "@/lib/ApiError";
import { getTransferById } from "./api";

export function useTransfer(transferId: string) {
  return useQuery({
    queryKey: ["transfers", "detail", transferId],
    queryFn: ({ signal }) => getTransferById(transferId, signal),
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