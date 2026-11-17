import type { PurchaseOrderStatus } from "@/types/api";

export const statusLabels: Record<PurchaseOrderStatus, string> = {
  1: "Draft",
  2: "Pending approval",
  3: "Approved",
  4: "Rejected",
  5: "Partially received",
  6: "Received",
};