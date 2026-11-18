import { apiRequest } from "@/lib/apiClient";
import type { GetPurchasingNotificationsResult } from "@/types/api";

const endpoint = "/api/purchasing-notifications";

export function getPurchasingNotifications(
  pageNumber: number,
  pageSize: number,
  signal?: AbortSignal,
): Promise<GetPurchasingNotificationsResult> {
  const query = new URLSearchParams({
    pageNumber: String(pageNumber),
    pageSize: String(pageSize),
  });

  return apiRequest<GetPurchasingNotificationsResult>(
    `${endpoint}?${query.toString()}`,
    { signal },
  );
}

export function markPurchasingNotificationAsRead(
  notificationId: string,
): Promise<void> {
  return apiRequest<void>(
    `${endpoint}/${encodeURIComponent(notificationId)}/read`,
    { method: "PUT" },
  );
}
