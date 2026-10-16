import { useQuery } from "@tanstack/react-query";
import { getWarehouses } from "./api";
import { ApiError } from "@/lib/ApiError";

export function useWarehouses() {
  return useQuery({
    queryKey: ["warehouses"],
    queryFn: ({ signal }) => getWarehouses(signal),
    staleTime: 60_000,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status < 500) {
        return false;
      }

      return failureCount < 1;
    },
  });
}
