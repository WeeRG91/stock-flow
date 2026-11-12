import type { UpdateDraftPurchaseOrderRequest } from "@/types/api";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { updateDraftPurchaseOrder } from "./api";

export function useUpdateDraftPurchaseOrder(orderId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: UpdateDraftPurchaseOrderRequest) =>
      updateDraftPurchaseOrder(orderId, request),
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
