import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ApiError } from "@/lib/ApiError";
import { createStockReservation } from "./api";

export function useCreateStockReservation() {
  const queryClient = useQueryClient();

  async function refreshQueries() {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: ["stock-reservations"],
      }),
      queryClient.invalidateQueries({
        queryKey: ["inventory"],
      }),
    ]);
  }

  return useMutation({
    mutationFn: createStockReservation,
    retry: false,
    onSuccess: refreshQueries,
    onError: async (error) => {
      if (error instanceof ApiError && error.status === 409) {
        await refreshQueries();
      }
    },
  });
}