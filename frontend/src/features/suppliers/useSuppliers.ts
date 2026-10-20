import { useQuery } from "@tanstack/react-query";
import { getSuppliers, type GetSuppliersParameters } from "./api";
import { ApiError } from "@/lib/ApiError";

export function useSuppliers(parameters: GetSuppliersParameters) {
  const normalizedParameters: GetSuppliersParameters = {
    pageNumber: parameters.pageNumber,
    pageSize: parameters.pageSize,
    search: parameters.search?.trim() || undefined,
    isActive: parameters.isActive,
  };

  return useQuery({
    queryKey: ["suppliers", "list", normalizedParameters],
    queryFn: ({ signal }) => getSuppliers(normalizedParameters, signal),
    staleTime: 0,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status < 500) {
        return false;
      }

      return failureCount < 1;
    },
  });
}
