import { useQuery } from "@tanstack/react-query";
import { ApiError } from "@/lib/ApiError";
import { getProductById } from "./api";

export function useProduct(productId: string) {
  return useQuery({
    queryKey: ["products", "detail", productId],
    queryFn: ({ signal }) => getProductById(productId, signal),
    enabled: productId.length > 0,
    staleTime: 0,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status < 500) {
        return false;
      }

      return failureCount < 1;
    },
  });
}