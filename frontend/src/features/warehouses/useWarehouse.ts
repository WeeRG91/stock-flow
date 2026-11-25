import { useQuery } from "@tanstack/react-query";
import { ApiError } from "@/lib/ApiError";
import { getWarehouseById } from "./api";

export function useWarehouse(warehouseId: string) {
  return useQuery({
    queryKey: ["warehouses", "detail", warehouseId],
    queryFn: ({ signal }) => getWarehouseById(warehouseId, signal),
    enabled: warehouseId.length > 0,
    staleTime: 0,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status < 500) {
        return false;
      }

      return failureCount < 1;
    },
  });
}