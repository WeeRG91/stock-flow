import { useMutation, useQueryClient } from "@tanstack/react-query";
import { markPurchasingNotificationAsRead } from "./api";

export function useMarkPurchasingNotificationAsRead(userId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: markPurchasingNotificationAsRead,
    retry: false,
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: ["purchasing-notifications", userId],
      });
    },
  });
}
