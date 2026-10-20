import { apiRequest } from "@/lib/apiClient";
import type {
  CreateSupplierRequest,
  CreateSupplierResult,
  GetSuppliersResult,
  SupplierDto,
  UpdateSupplierRequest,
} from "@/types/api";

export interface GetSuppliersParameters {
  pageNumber: number;
  pageSize: number;
  search?: string;
  isActive?: boolean;
}

export function getSuppliers(
  parameters: GetSuppliersParameters,
  signal?: AbortSignal,
): Promise<GetSuppliersResult> {
  const query = new URLSearchParams({
    pageNumber: String(parameters.pageNumber),
    pageSize: String(parameters.pageSize),
  });

  const search = parameters.search?.trim();

  if (search) {
    query.set("search", search);
  }

  if (parameters.isActive !== undefined) {
    query.set("isActive", String(parameters.isActive));
  }

  return apiRequest<GetSuppliersResult>(`/api/suppliers?${query.toString()}`, {
    signal,
  });
}

export function getSupplierById(
  supplierId: string,
  signal?: AbortSignal,
): Promise<SupplierDto> {
  return apiRequest<SupplierDto>(
    `/api/suppliers/${encodeURIComponent(supplierId)}`,
    { signal },
  );
}

export function createSupplier(
  request: CreateSupplierRequest,
): Promise<CreateSupplierResult> {
  return apiRequest<CreateSupplierResult>("/api/suppliers", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(request),
  });
}

export function updateSupplier(
  supplierId: string,
  request: UpdateSupplierRequest,
): Promise<SupplierDto> {
  return apiRequest<SupplierDto>(
    `/api/suppliers/${encodeURIComponent(supplierId)}`,
    {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(request),
    },
  );
}

export function activateSupplier(supplierId: string): Promise<SupplierDto> {
  return apiRequest<SupplierDto>(
    `/api/suppliers/${encodeURIComponent(supplierId)}/activate`,
    {
      method: "PATCH",
    },
  );
}

export function deactivateSupplier(supplierId: string): Promise<SupplierDto> {
  return apiRequest<SupplierDto>(
    `/api/suppliers/${encodeURIComponent(supplierId)}/deactivate`,
    {
      method: "PATCH",
    },
  );
}
