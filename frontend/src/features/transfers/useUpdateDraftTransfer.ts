import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { UpdateDraftTransferRequest } from "@/types/api";
import { updateDraftTransfer } from "./api";
import { ApiError } from "@/lib/ApiError";

export function useUpdateDraftTransfer(transferId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: UpdateDraftTransferRequest) =>
      updateDraftTransfer(transferId, request),
    retry: false,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: ["transfers", "detail", transferId],
        }),
        queryClient.invalidateQueries({
          queryKey: ["transfers", "list"],
        }),
      ]);
    },
    onError: async (error) => {
      if (error instanceof ApiError && error.status === 409) {
        await Promise.all([
          queryClient.invalidateQueries({
            queryKey: ["transfers", "detail", transferId],
          }),
          queryClient.invalidateQueries({
            queryKey: ["transfers", "list"],
          }),
        ]);
      }
    },
  });
}
