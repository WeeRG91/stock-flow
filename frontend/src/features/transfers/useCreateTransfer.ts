import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createTransfer } from "./api";

export function useCreateTransfer() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: createTransfer,
    retry: false,
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: ["transfers", "list"],
      });
    },
  });
}
