import { apiRequest } from "@/lib/apiClient";
import type {
  CreateStockReservationRequest,
  CreateStockReservationResult,
  GetStockReservationsResult,
  StockReservationDto,
  StockReservationStatus,
  StockReservationVersionRequest,
} from "@/types/api";

export interface GetStockReservationParameters {
  pageNumber: number;
  pageSize: number;
  status?: StockReservationStatus;
  search?: string;
  inventoryId?: string;
}

export function getStockReservations(
  {
    pageNumber,
    pageSize,
    status,
    search,
    inventoryId,
  }: GetStockReservationParameters,
  signal?: AbortSignal,
): Promise<GetStockReservationsResult> {
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

  if (inventoryId) {
    query.set("inventoryId", inventoryId);
  }

  return apiRequest<GetStockReservationsResult>(
    `/api/stock-reservations?${query.toString()}`,
    { signal },
  );
}

export function getStockReservationById(
  reservationId: string,
  signal?: AbortSignal,
): Promise<StockReservationDto> {
  return apiRequest<StockReservationDto>(
    `/api/stock-reservations/${encodeURIComponent(reservationId)}`,
    { signal },
  );
}

export function createStockReservation(
  request: CreateStockReservationRequest,
): Promise<CreateStockReservationResult> {
  return apiRequest<CreateStockReservationResult>("/api/stock-reservations", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
}

export function releaseStockReservation(
  reservationId: string,
  request: StockReservationVersionRequest,
): Promise<void> {
  return apiRequest<void>(
    `/api/stock-reservations/${encodeURIComponent(reservationId)}/release`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    },
  );
}

export function fulfillStockReservation(
  reservationId: string,
  request: StockReservationVersionRequest,
): Promise<void> {
  return apiRequest<void>(
    `/api/stock-reservations/${encodeURIComponent(reservationId)}/fulfill`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    },
  );
}
