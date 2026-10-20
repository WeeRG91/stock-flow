import { useQuery } from "@tanstack/react-query";
import { getSupplierById } from "./api";
import { ApiError } from "@/lib/ApiError";

export function useSupplier(supplierId: string) {
  return useQuery({
    queryKey: ["suppliers", "detail", supplierId],
    queryFn: ({ signal }) => getSupplierById(supplierId, signal),
    enabled: supplierId.length > 0,
    staleTime: 0,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status < 500) {
        return false;
      }

      return failureCount < 1;
    },
  });
}
