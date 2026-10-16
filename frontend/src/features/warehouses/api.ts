import { apiRequest } from "@/lib/apiClient";
import type { WarehouseDto } from "@/types/api";

export function getWarehouses(signal?: AbortSignal): Promise<WarehouseDto[]> {
  return apiRequest<WarehouseDto[]>("/api/warehouses", { signal });
}
