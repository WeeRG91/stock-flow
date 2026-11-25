import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ApiError } from "@/lib/ApiError";
import type { ReceiveTransferRequest } from "@/types/api";
import { receiveTransfer } from "./api";

export function useReceiveTransfer(transferId: string) {
  const queryClient = useQueryClient();

  async function refreshAffectedData() {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: ["transfers", "detail", transferId],
      }),
      queryClient.invalidateQueries({
        queryKey: ["transfers", "list"],
      }),
      queryClient.invalidateQueries({
        queryKey: ["transfers", "receipt-history", transferId],
      }),
      queryClient.invalidateQueries({
        queryKey: ["inventory"],
      }),
    ]);
  }

  return useMutation({
    mutationFn: (request: ReceiveTransferRequest) =>
      receiveTransfer(transferId, request),
    retry: false,
    onSuccess: refreshAffectedData,
    onError: async (error) => {
      if (error instanceof ApiError && error.status === 409) {
        await refreshAffectedData();
      }
    },
  });
}