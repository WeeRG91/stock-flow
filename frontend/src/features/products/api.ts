import { apiRequest } from "@/lib/apiClient";
import type { ProductDto } from "@/types/api";

export function getProducts(signal?: AbortSignal): Promise<ProductDto[]> {
  return apiRequest<ProductDto[]>("/api/products", { signal });
}
