import { useQuery } from "@tanstack/react-query";
import { getStockReservations, type GetStockReservationParameters } from "./api";
import { ApiError } from "@/lib/ApiError";

export function useStockReservations(
    parameters: GetStockReservationParameters,
) {
    return useQuery({
    queryKey: ["stock-reservations", "list", parameters],
    queryFn: ({ signal }) => getStockReservations(parameters, signal),
    staleTime: 0,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status < 500) {
        return false;
      }

      return failureCount < 1;
    },
  });
}