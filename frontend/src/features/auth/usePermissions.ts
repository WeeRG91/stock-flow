import type { CurrentUserResponse } from "@/types/api";
import { useOutletContext } from "react-router";

type AuthenticatedOutletContext = {
  user: CurrentUserResponse;
};

type Capability =
  | "Catalog.Read"
  | "Inventory.Read"
  | "Inventory.Receive"
  | "Suppliers.Read"
  | "Suppliers.Manage"
  | "BackgroundJobs.Manage"
  | "PurchaseOrders.Manage"
  | "PurchaseOrders.Approve";

export function usePermissions() {
  const { user } = useOutletContext<AuthenticatedOutletContext>();

  function can(capability: Capability): boolean {
    return user.capabilities.includes(capability);
  }

  return { can, user };
}
