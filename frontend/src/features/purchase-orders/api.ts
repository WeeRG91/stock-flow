import { apiRequest } from "@/lib/apiClient";
import type {
  CreatePurchaseOrderRequest,
  CreatePurchaseOrderResult,
  GetPurchaseOrdersResult,
  PurchaseOrderDetailsDto,
  UpdateDraftPurchaseOrderRequest,
} from "@/types/api";

export interface GetPurchaseOrdersParameters {
  pageNumber: number;
  pageSize: number;
}

export function getPurchaseOrders(
  { pageNumber, pageSize }: GetPurchaseOrdersParameters,
  signal?: AbortSignal,
): Promise<GetPurchaseOrdersResult> {
  const query = new URLSearchParams({
    pageNumber: String(pageNumber),
    pageSize: String(pageSize),
  });

  return apiRequest<GetPurchaseOrdersResult>(
    `/api/purchase-orders?${query.toString()}`,
    { signal },
  );
}

export function createPurchaseOrder(
  request: CreatePurchaseOrderRequest,
): Promise<CreatePurchaseOrderResult> {
  return apiRequest<CreatePurchaseOrderResult>("/api/purchase-orders", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
}

export function getPurchaseOrderById(
  orderId: string,
  signal?: AbortSignal,
): Promise<PurchaseOrderDetailsDto> {
  return apiRequest<PurchaseOrderDetailsDto>(
    `/api/purchase-orders/${encodeURIComponent(orderId)}`,
    { signal },
  );
}

export function updateDraftPurchaseOrder(
  orderId: string,
  request: UpdateDraftPurchaseOrderRequest,
): Promise<void> {
  return apiRequest<void>(
    `/api/purchase-orders/${encodeURIComponent(orderId)}`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    },
  );
}

export function submitPurchaseOrder(
  orderId: string,
  expectedVersion: string,
): Promise<void> {
  return apiRequest<void>(
    `/api/purchase-orders/${encodeURIComponent(orderId)}/submit`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ expectedVersion }),
    },
  );
}

export function decidePurchaseOrder(
  orderId: string,
  decision: "approve" | "reject",
  expectedVersion: string,
): Promise<void> {
  return apiRequest<void>(
    `/api/purchase-orders/${encodeURIComponent(orderId)}/${decision}`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ expectedVersion }),
    },
  );
}
