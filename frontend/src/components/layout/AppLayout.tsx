import { useId, useState, type ReactNode } from "react";
import { Link } from "react-router";
import { AppNavigation } from "./AppNavigation";
import { Button } from "@/components/ui/button";
import { Menu, X } from "lucide-react";

type AppLayoutProps = {
  children: ReactNode;
};

export function AppLayout({ children }: AppLayoutProps) {
  const [navigationOpen, setNavigationOpen] = useState<boolean>(false);
  const navigationId = useId();

  return (
    <div className="min-h-screen bg-muted/30 lg:grid lg:grid-cols-[16rem_minmax(0,1fr)]">
      <a
        href="#main-content"
        className="sr-only z-50 rounded-md bg-background px-4 py-2 text-foreground focus:not-sr-only focus:fixed focus:left-4 focus:top-4"
      >
        Skip to main content
      </a>

      <aside className="border-b bg-sidebar text-sidebar-foreground lg:sticky lg:top-0 lg:h-screen lg:border-b-0 lg:border-r">
        <div className="flex items-center justify-between gap-4 border-b px-6 py-5">
          <div>
            <Link
              to="/"
              className="rounded-sm text-xl font-semibold tracking-tight focus-visible:outline-2 focus-visible:outline-offset-4"
            >
              StockFlow
            </Link>
            <p className="mt-1 text-xs text-muted-foreground">
              NovaTrade operations
            </p>
          </div>

          <Button
            type="button"
            variant="ghost"
            size="icon"
            className="size-11 shrink-0 lg:hidden"
            aria-label={navigationOpen ? "Hide navigation" : "Show navigation"}
            aria-expanded={navigationOpen}
            aria-controls={navigationId}
            onClick={() => setNavigationOpen((open) => !open)}
          >
            {navigationOpen ? (
              <X className="size-5" aria-hidden="true" />
            ) : (
              <Menu className="size-5" aria-hidden="true" />
            )}
          </Button>
        </div>

        <div
          id={navigationId}
          className={[
            "grid transition-[grid-template-rows,visibility] duration-200 ease-in-out",
            "motion-reduce:transition-none",
            "lg:visible lg:grid-rows-[1fr]",
            navigationOpen
              ? "visible grid-rows-[1fr]"
              : "invisible grid-rows-[0fr]",
          ].join(" ")}
        >
          <div className="min-h-0 overflow-hidden">
            <div className="p-4">
              <AppNavigation />
            </div>
          </div>
        </div>
      </aside>

      <div className="min-w-0">
        <header className="flex min-h-16 items-center justify-between gap-4 border-b bg-background px-4 sm:px-6 lg:px-8">
          <span className="text-sm font-medium">Inventory management</span>
          <span className="text-xs text-muted-foreground">NovaTrade</span>
        </header>

        <main
          id="main-content"
          tabIndex={-1}
          className="mx-auto w-full max-w-7xl space-y-6 p-4 sm:p-6 lg:p-8"
        >
          {children}
        </main>
      </div>
    </div>
  );
}
