export interface ProductDto {
  id: string;
  name: string;
  sku: string;
  description: string | null;
  isActive: boolean;
}

export interface WarehouseDto {
  id: string;
  code: string;
  name: string;
  location: string;
  isActive: boolean;
}

export interface InventoryBalanceDto {
  inventoryId: string;
  productId: string;
  productName: string;
  sku: string;
  warehouseId: string;
  warehouseName: string;
  warehouseCode: string;
  quantityOnHand: number;
}

export interface GetInventoryResult {
  items: InventoryBalanceDto[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

export interface ReceiveStockRequest {
  productId: string;
  warehouseId: string;
  quantity: number;
}

export interface ReceiveStockResult {
  inventoryId: string;
  quantityOnHand: number;
}

export interface ApiProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

export interface SupplierDto {
  id: string;
  code: string;
  name: string;
  contactEmail: string;
  phoneNumber: string | null;
  isActive: boolean;
}

export interface GetSuppliersResult {
  items: SupplierDto[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

export interface CreateSupplierRequest {
  code: string;
  name: string;
  contactEmail: string;
  phoneNumber: string | null;
}

export interface CreateSupplierResult {
  supplierId: string;
  code: string;
  name: string;
  contactEmail: string;
  phoneNumber: string | null;
  isActive: boolean;
}

export interface UpdateSupplierRequest {
  name: string;
  contactEmail: string;
  phoneNumber: string | null;
}