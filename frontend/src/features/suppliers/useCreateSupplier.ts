import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createSupplier } from "./api";

export function useCreateSupplier() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: createSupplier,
    retry: false,
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: ["suppliers", "list"],
      });
    },
  });
}
