import { useMutation, useQueryClient } from "@tanstack/react-query";
import { receiveStock } from "./api";
import { ApiError } from "@/lib/ApiError";

export function useReceiveStock() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: receiveStock,
    retry: false,
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: ["inventory"],
      });
    },
    onError: async (error) => {
      if (error instanceof ApiError && error.status === 409) {
        await queryClient.invalidateQueries({
          queryKey: ["inventory"],
        });
      }
    },
  });
}
