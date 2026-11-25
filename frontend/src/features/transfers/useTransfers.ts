import { useQuery } from "@tanstack/react-query";
import { getTransfers, type GetTransfersParameters } from "./api";
import { ApiError } from "@/lib/ApiError";

export function useTransfers(parameters: GetTransfersParameters) {
  return useQuery({
    queryKey: ["transfers", "list", parameters],
    queryFn: ({ signal }) => getTransfers(parameters, signal),
    staleTime: 0,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status < 500) {
        return false;
      }

      return failureCount < 1;
    },
  });
}
