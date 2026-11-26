import { useQuery } from "@tanstack/react-query";
import { ApiError } from "@/lib/ApiError";
import { getInventory } from "@/features/inventory/api";

export function useReservationAvailability(
  productId: string,
  warehouseId: string,
) {
  const filters = { productId, warehouseId };

  return useQuery({
    queryKey: [
      "inventory",
      { pageNumber: 1, pageSize: 1, ...filters },
    ],
    queryFn: ({ signal }) => getInventory(1, 1, filters, signal),
    enabled: productId.length > 0 && warehouseId.length > 0,
    staleTime: 0,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status < 500) {
        return false;
      }

      return failureCount < 1;
    },
  });
}