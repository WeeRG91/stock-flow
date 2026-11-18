import { useQuery } from "@tanstack/react-query";
import { getPurchasingNotifications } from "./api";
import { ApiError } from "@/lib/ApiError";

export function usePurchasingNotifications(
  userId: string,
  pageNumber = 1,
  pageSize = 20,
) {
  return useQuery({
    queryKey: ["purchasing-notifications", userId, { pageNumber, pageSize }],
    queryFn: ({ signal }) =>
      getPurchasingNotifications(pageNumber, pageSize, signal),
    enabled: userId.length > 0,
    retry: (failureCount, error) =>
      !(error instanceof ApiError && error.status < 500) && failureCount < 1,
  });
}
