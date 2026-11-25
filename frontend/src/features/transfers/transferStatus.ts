import type { TransferStatus } from "@/types/api";

export const transferStatusLabels: Record<TransferStatus, string> = {
  1: "Draft",
  2: "Dispatched",
  3: "Partially received",
  4: "Received",
  5: "Cancelled",
};
