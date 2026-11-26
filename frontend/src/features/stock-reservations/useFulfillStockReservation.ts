import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ApiError } from "@/lib/ApiError";
import { fulfillStockReservation } from "./api";

type FulfillVariables = {
  reservationId: string;
  expectedVersion: string;
};

export function useFulfillStockReservation() {
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
    mutationFn: ({
      reservationId,
      expectedVersion,
    }: FulfillVariables) =>
      fulfillStockReservation(reservationId, { expectedVersion }),
    retry: false,
    onSuccess: refreshQueries,
    onError: async (error) => {
      if (error instanceof ApiError && error.status === 409) {
        await refreshQueries();
      }
    },
  });
}