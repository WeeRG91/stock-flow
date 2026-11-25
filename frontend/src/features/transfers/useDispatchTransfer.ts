import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ApiError } from "@/lib/ApiError";
import { dispatchTransfer } from "./api";

export function useDispatchTransfer(transferId: string) {
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
        queryKey: ["inventory"],
      }),
    ]);
  }

  return useMutation({
    mutationFn: (expectedVersion: string) =>
      dispatchTransfer(transferId, expectedVersion),
    retry: false,
    onSuccess: refreshAffectedData,
    onError: async (error) => {
      if (error instanceof ApiError && error.status === 409) {
        await refreshAffectedData();
      }
    },
  });
}