import { useMutation, useQueryClient } from "@tanstack/react-query";
import { submitPurchaseOrder } from "./api";

export function useSubmitPurchaseOrder(orderId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (expectedVersion: string) =>
      submitPurchaseOrder(orderId, expectedVersion),
    retry: false,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: ["purchase-orders", "detail", orderId],
        }),
        queryClient.invalidateQueries({
          queryKey: ["purchase-orders", "list"],
        }),
      ]);
    },
  });
}
