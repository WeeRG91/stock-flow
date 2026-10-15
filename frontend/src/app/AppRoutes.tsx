import { Route, Routes } from "react-router";
import { DashboardPage } from "../features/dashboard/DashboardPage";
import { InventoryPage } from "../features/inventory/InventoryPage";
import { ProductsPage } from "../features/products/ProductsPage";
import { WarehousesPage } from "../features/warehouses/WarehousesPage";
import { NotFoundPage } from "./NotFoundPage";

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<DashboardPage />} />
      <Route path="/inventory" element={<InventoryPage />} />
      <Route path="/products" element={<ProductsPage />} />
      <Route path="/warehouses" element={<WarehousesPage />} />
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  );
}
