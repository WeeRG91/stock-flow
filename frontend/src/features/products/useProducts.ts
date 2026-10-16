import { useQuery } from "@tanstack/react-query";
import { getProducts } from "./api";
import { ApiError } from "@/lib/ApiError";

export function useProducts() {
  return useQuery({
    queryKey: ["products"],
    queryFn: ({ signal }) => getProducts(signal),
    staleTime: 60_000,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status < 500) {
        return false;
      }

      return failureCount < 1;
    },
  });
}
