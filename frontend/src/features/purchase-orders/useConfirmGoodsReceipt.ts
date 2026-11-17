import type { ConfirmGoodsReceiptRequest } from "@/types/api";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { confirmGoodsReceipt } from "./api";

export function useConfirmGoodsReceipt(orderId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: ConfirmGoodsReceiptRequest) =>
      confirmGoodsReceipt(orderId, request),
    retry: false,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: ["purchase-orders", "detail", orderId],
        }),
        queryClient.invalidateQueries({
          queryKey: ["purchase-orders", "list"],
        }),
        queryClient.invalidateQueries({ queryKey: ["inventory"] }),
        queryClient.invalidateQueries({
          queryKey: ["purchase-orders", "receipt-history", orderId],
        }),
      ]);
    },
  });
}
