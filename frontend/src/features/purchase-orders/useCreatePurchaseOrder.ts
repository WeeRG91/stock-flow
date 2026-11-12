import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createPurchaseOrder } from "./api";

export function useCreatePurchaseOrder() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: createPurchaseOrder,
    retry: false,
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: ["purchase-orders", "list"],
      });
    },
  });
}
