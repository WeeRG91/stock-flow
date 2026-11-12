import { useMutation, useQueryClient } from "@tanstack/react-query";
import { decidePurchaseOrder } from "./api";

type DecisionRequest = {
  decision: "approve" | "reject";
  expectedVersion: string;
};

export function useDecidePurchaseOrder(orderId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ decision, expectedVersion }: DecisionRequest) =>
      decidePurchaseOrder(orderId, decision, expectedVersion),
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