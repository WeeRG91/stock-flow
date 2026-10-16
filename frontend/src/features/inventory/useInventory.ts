import { useQuery } from "@tanstack/react-query";
import { ApiError } from "@/lib/ApiError";
import { getInventory } from "./api";

export function useInventory(pageNumber: number, pageSize: number) {
  return useQuery({
    queryKey: ["inventory", { pageNumber, pageSize }],
    queryFn: ({ signal }) => getInventory(pageNumber, pageSize, signal),
    staleTime: 0,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status < 500) {
        return false;
      }

      return failureCount < 1;
    },
  });
}
