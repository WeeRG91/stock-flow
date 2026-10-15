import { NavLink } from "react-router";

const navigationItems = [
  { to: "/", label: "Dashboard" },
  { to: "/inventory", label: "Inventory" },
  { to: "/products", label: "Products" },
  { to: "/warehouses", label: "Warehouses" },
];

export function AppNavigation() {
  return (
    <nav aria-label="Main navigation">
      <ul className="space-y-1">
        {navigationItems.map((item) => (
          <li key={item.to}>
            <NavLink
              to={item.to}
              end={item.to === "/"}
              className={({ isActive }) =>
                [
                  "flex min-h-11 items-center rounded-lg px-3 py-2 text-sm font-medium",
                  "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring",
                  isActive
                    ? "bg-sidebar-accent text-sidebar-accent-foreground"
                    : "text-muted-foreground hover:bg-sidebar-accent/60 hover:text-sidebar-foreground",
                ].join(" ")
              }
            >
              {item.label}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}