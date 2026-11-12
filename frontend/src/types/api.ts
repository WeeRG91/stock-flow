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

export interface LoginRequest {
  email: string;
  password: string;
}

export interface UserProfileDto {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
}

export interface AntiforgeryTokenResponse {
  requestToken: string;
  headerName: string;
}

export interface CurrentUserResponse extends UserProfileDto {
  capabilities: string[];
}

export type PurchaseOrderStatus = 1 | 2 | 3 | 4 | 5 | 6;

export interface PurchaseOrderSummaryDto {
  id: string;
  orderNumber: string;
  supplierCode: string;
  supplierName: string;
  warehouseCode: string;
  warehouseName: string;
  status: PurchaseOrderStatus;
  createdAt: string;
  lineCount: number;
}

export interface GetPurchaseOrdersResult {
  items: PurchaseOrderSummaryDto[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

export interface CreatePurchaseOrderLineRequest {
  productId: string;
  orderedQuantity: number;
  unitPrice: number;
}

export interface CreatePurchaseOrderRequest {
  supplierId: string;
  warehouseId: string;
  lines: CreatePurchaseOrderLineRequest[];
}

export interface CreatePurchaseOrderResult {
  purchaseOrderId: string;
  orderNumber: string;
  status: PurchaseOrderStatus;
  currency: string;
  totalAmount: number;
  createdAt: string;
  createdByUserId: string;
}

export interface PurchaseOrderLineDto {
  id: string;
  productId: string;
  productSku: string;
  productName: string;
  orderedQuantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface PurchaseOrderDetailsDto {
  id: string;
  orderNumber: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  status: PurchaseOrderStatus;
  createdAt: string;
  createdByUserId: string;
  submittedAt: string | null;
  approvedAt: string | null;
  rejectedAt: string | null;
  approvedByUserId: string | null;
  rejectedByUserId: string | null;
  version: string;
  currency: string;
  lines: PurchaseOrderLineDto[];
  totalAmount: number;
}

export interface UpdateDraftPurchaseOrderRequest {
  expectedVersion: string;
  lines: CreatePurchaseOrderLineRequest[];
}
