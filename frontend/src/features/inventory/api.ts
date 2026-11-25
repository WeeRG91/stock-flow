import { apiRequest } from "@/lib/apiClient";
import type {
  GetInventoryResult,
  ReceiveStockRequest,
  ReceiveStockResult,
} from "@/types/api";

export type InventoryFilters = {
  productId?: string;
  warehouseId?: string;
};

export function getInventory(
  pageNumber: number,
  pageSize: number,
  filters: InventoryFilters = {},
  signal?: AbortSignal,
): Promise<GetInventoryResult> {
  const parameters = new URLSearchParams({
    pageNumber: String(pageNumber),
    pageSize: String(pageSize),
  });

  if (filters.productId) {
    parameters.set("productId", filters.productId);
  }

  if (filters.warehouseId) {
    parameters.set("warehouseId", filters.warehouseId);
  }

  return apiRequest<GetInventoryResult>(
    `/api/inventory?${parameters.toString()}`,
    { signal },
  );
}

export function receiveStock(
  request: ReceiveStockRequest,
): Promise<ReceiveStockResult> {
  return apiRequest<ReceiveStockResult>("/api/inventory/receive", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(request),
  });
}
