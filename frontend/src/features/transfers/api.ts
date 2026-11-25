import { apiRequest } from "@/lib/apiClient";
import type {
  CreateTransferRequest,
  CreateTransferResult,
  GetTransfersResult,
  ReceiveTransferRequest,
  ReceiveTransferResult,
  TransferDetailsDto,
  TransferReceiptHistoryItemDto,
  TransferStatus,
  UpdateDraftTransferRequest,
} from "@/types/api";

export interface GetTransfersParameters {
  pageNumber: number;
  pageSize: number;
  status?: TransferStatus;
  search?: string;
}

export function getTransfers(
  { pageNumber, pageSize, status, search }: GetTransfersParameters,
  signal?: AbortSignal,
): Promise<GetTransfersResult> {
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

  return apiRequest<GetTransfersResult>(`/api/transfers?${query.toString()}`, {
    signal,
  });
}

export function getTransferById(
  transferId: string,
  signal?: AbortSignal,
): Promise<TransferDetailsDto> {
  return apiRequest<TransferDetailsDto>(
    `/api/transfers/${encodeURIComponent(transferId)}`,
    { signal },
  );
}

export function createTransfer(
  request: CreateTransferRequest,
): Promise<CreateTransferResult> {
  return apiRequest<CreateTransferResult>("/api/transfers", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
}

export function updateDraftTransfer(
  transferId: string,
  request: UpdateDraftTransferRequest,
): Promise<void> {
  return apiRequest<void>(`/api/transfers/${encodeURIComponent(transferId)}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
}

export function cancelDraftTransfer(
  transferId: string,
  expectedVersion: string,
): Promise<void> {
  return apiRequest<void>(
    `/api/transfers/${encodeURIComponent(transferId)}/cancel`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ expectedVersion }),
    },
  );
}

export function dispatchTransfer(
  transferId: string,
  expectedVersion: string,
): Promise<void> {
  return apiRequest<void>(
    `/api/transfers/${encodeURIComponent(transferId)}/dispatch`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ expectedVersion }),
    },
  );
}

export function receiveTransfer(
  transferId: string,
  request: ReceiveTransferRequest,
): Promise<ReceiveTransferResult> {
  return apiRequest<ReceiveTransferResult>(
    `/api/transfers/${encodeURIComponent(transferId)}/receipts`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    },
  );
}

export function getTransferReceiptHistory(
  transferId: string,
  signal?: AbortSignal,
): Promise<TransferReceiptHistoryItemDto[]> {
  return apiRequest<TransferReceiptHistoryItemDto[]>(
    `/api/transfers/${encodeURIComponent(transferId)}/receipts`,
    { signal },
  );
}
