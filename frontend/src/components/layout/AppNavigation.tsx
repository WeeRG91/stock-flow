import {
  Boxes,
  LayoutDashboard,
  Package,
  Truck,
  Warehouse,
} from "lucide-react";
import { NavLink } from "react-router";

const navigationItems = [
  { to: "/", label: "Dashboard", icon: LayoutDashboard },
  { to: "/inventory", label: "Inventory", icon: Boxes },
  { to: "/products", label: "Products", icon: Package },
  { to: "/warehouses", label: "Warehouses", icon: Warehouse },
  { to: "/suppliers", label: "Suppliers", icon: Truck },
];

export function AppNavigation() {
  return (
    <nav aria-label="Main navigation">
      <p className="mb-3 px-3 text-xs font-semibold tracking-wide text-muted-foreground">
        Workspace
      </p>

      <ul className="space-y-1.5">
        {navigationItems.map(({ to, label, icon: Icon }) => (
          <li key={to}>
            <NavLink
              to={to}
              end={to === "/"}
              className={({ isActive }) =>
                [
                  "flex min-h-11 items-center gap-3 rounded-xl px-3 py-2.5 text-sm",
                  "transition-colors duration-150 motion-reduce:transition-none",
                  "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring",
                  isActive
                    ? "bg-sidebar-accent font-semibold text-sidebar-accent-foreground"
                    : "font-medium text-muted-foreground hover:bg-muted hover:text-sidebar-foreground",
                ].join(" ")
              }
            >
              <Icon
                aria-hidden="true"
                className="size-5 shrink-0"
                strokeWidth={1.75}
              />

              <span>{label}</span>
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}