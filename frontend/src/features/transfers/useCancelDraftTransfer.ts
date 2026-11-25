import { useMutation, useQueryClient } from "@tanstack/react-query";
import { cancelDraftTransfer } from "./api";
import { ApiError } from "@/lib/ApiError";

export function useCancelDraftTransfer(transferId: string) {
  const queryClient = useQueryClient();

  async function refreshTransfers() {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: ["transfers", "detail", transferId],
      }),
      queryClient.invalidateQueries({
        queryKey: ["transfers", "list"],
      }),
    ]);
  }

  return useMutation({
    mutationFn: (expectedVersion: string) =>
      cancelDraftTransfer(transferId, expectedVersion),
    retry: false,
    onSuccess: refreshTransfers,
    onError: async (error) => {
      if (error instanceof ApiError && error.status === 409) {
        await refreshTransfers();
      }
    },
  });
}
