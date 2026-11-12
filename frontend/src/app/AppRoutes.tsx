import { Route, Routes } from "react-router";
import { DashboardPage } from "../features/dashboard/DashboardPage";
import { InventoryPage } from "../features/inventory/InventoryPage";
import { ProductsPage } from "../features/products/ProductsPage";
import { WarehousesPage } from "../features/warehouses/WarehousesPage";
import { NotFoundPage } from "./NotFoundPage";
import { SuppliersPage } from "@/features/suppliers/SuppliersPage";
import { SupplierDetailsPage } from "@/features/suppliers/SupplierDetailsPage";
import { ProtectedLayout } from "./ProtectedLayout";
import { LoginPage } from "@/features/auth/LoginPage";
import { PurchaseOrdersPage } from "@/features/purchase-orders/PurchaseOrdersPage";
import { PurchaseOrderDetailsPage } from "@/features/purchase-orders/PurchaseOrderDetailsPage";

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />

      <Route element={<ProtectedLayout />}>
        <Route path="/" element={<DashboardPage />} />
        <Route path="/inventory" element={<InventoryPage />} />
        <Route path="/products" element={<ProductsPage />} />
        <Route path="/warehouses" element={<WarehousesPage />} />
        <Route path="/suppliers" element={<SuppliersPage />} />
        <Route path="/suppliers/:id" element={<SupplierDetailsPage />} />
        <Route path="/purchase-orders" element={<PurchaseOrdersPage />} />
        <Route path="/purchase-orders/:id" element={<PurchaseOrderDetailsPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}
