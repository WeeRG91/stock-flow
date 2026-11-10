import { ArrowUpRight, Boxes, Package, Truck, Warehouse } from "lucide-react";
import { Link } from "react-router";
import { PageHeader } from "@/components/PageHeader";

const sections = [
  {
    to: "/inventory",
    title: "Inventory",
    description: "Review stock balances and record incoming stock.",
    icon: Boxes,
  },
  {
    to: "/products",
    title: "Products",
    description: "Browse product names, SKUs, and descriptions.",
    icon: Package,
  },
  {
    to: "/warehouses",
    title: "Warehouses",
    description: "Find warehouse locations and their current status.",
    icon: Warehouse,
  },
  {
    to: "/suppliers",
    title: "Suppliers",
    description: "View suppliers and manage their contact information.",
    icon: Truck,
  },
];

export function DashboardPage() {
  return (
    <div className="space-y-8">
      <PageHeader
        title="Dashboard"
        description="Your workspace for NovaTrade's inventory and supplier operations."
      />

      <section aria-labelledby="workspace-heading" className="space-y-4">
        <div>
          <h2 id="workspace-heading" className="text-base font-semibold">
            Explore your workspace
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Choose an area to get started.
          </p>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          {sections.map(({ to, title, description, icon: Icon }) => (
            <Link
              key={to}
              to={to}
              className="group flex items-start gap-4 rounded-xl border border-border bg-card p-6 shadow-sm transition-colors hover:border-primary/40 hover:bg-accent/30 focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring motion-reduce:transition-none"
            >
              <span className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-accent text-accent-foreground">
                <Icon aria-hidden="true" className="size-6" strokeWidth={1.75} />
              </span>

              <div className="min-w-0 flex-1">
                <h3 className="font-semibold">{title}</h3>
                <p className="mt-2 text-sm leading-6 text-muted-foreground">
                  {description}
                </p>
              </div>

              <ArrowUpRight
                aria-hidden="true"
                className="size-5 shrink-0 text-muted-foreground group-hover:text-primary"
              />
            </Link>
          ))}
        </div>
      </section>
    </div>
  );
}