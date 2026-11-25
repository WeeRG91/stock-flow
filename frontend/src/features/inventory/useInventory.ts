import { useQuery } from "@tanstack/react-query";
import { ApiError } from "@/lib/ApiError";
import { getInventory, type InventoryFilters } from "./api";

export function useInventory(
  pageNumber: number,
  pageSize: number,
  filters: InventoryFilters = {},
) {
  return useQuery({
    queryKey: ["inventory", { pageNumber, pageSize, ...filters }],
    queryFn: ({ signal }) =>
      getInventory(pageNumber, pageSize, filters, signal),
    staleTime: 0,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status < 500) {
        return false;
      }

      return failureCount < 1;
    },
  });
}
