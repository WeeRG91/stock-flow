import { apiRequest } from "@/lib/apiClient";
import type {
  ConfirmGoodsReceiptRequest,
  ConfirmGoodsReceiptResult,
  CreatePurchaseOrderRequest,
  CreatePurchaseOrderResult,
  GetPurchaseOrdersResult,
  GoodsReceiptHistoryItemDto,
  PurchaseOrderDetailsDto,
  PurchaseOrderStatus,
  UpdateDraftPurchaseOrderRequest,
} from "@/types/api";

export interface GetPurchaseOrdersParameters {
  pageNumber: number;
  pageSize: number;
  status?: PurchaseOrderStatus;
  search?: string;
}

export function getPurchaseOrders(
  { pageNumber, pageSize, status, search }: GetPurchaseOrdersParameters,
  signal?: AbortSignal,
): Promise<GetPurchaseOrdersResult> {
  const query = new URLSearchParams({
    pageNumber: String(pageNumber),
    pageSize: String(pageSize),
  });

  if (status !== undefined) {
    query.set("status", String(status));
  }

  if (search) {
    query.set("search", search);
  }

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

export function confirmGoodsReceipt(
  orderId: string,
  request: ConfirmGoodsReceiptRequest,
): Promise<ConfirmGoodsReceiptResult> {
  return apiRequest<ConfirmGoodsReceiptResult>(
    `/api/purchase-orders/${encodeURIComponent(orderId)}/receipts`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    },
  );
}

export function getGoodsReceiptHistory(
  orderId: string,
  signal?: AbortSignal,
): Promise<GoodsReceiptHistoryItemDto[]> {
  return apiRequest<GoodsReceiptHistoryItemDto[]>(
    `/api/purchase-orders/${encodeURIComponent(orderId)}/receipts`,
    { signal },
  );
}
