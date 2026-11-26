import { useQuery } from "@tanstack/react-query";
import { getStockReservationById } from "./api";
import { ApiError } from "@/lib/ApiError";

export function useStockReservation(reservationId: string) {
  return useQuery({
    queryKey: ["stock-reservations", "detail", reservationId],
    queryFn: ({ signal }) => getStockReservationById(reservationId, signal),
    enabled: reservationId.length > 0,
    staleTime: 0,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status < 500) {
        return false;
      }

      return failureCount < 1;
    },
  });
}
