import { useId, useState, type ReactNode } from "react";
import { Link } from "react-router";
import { AppNavigation } from "./AppNavigation";
import { Button } from "@/components/ui/button";
import { Boxes, LogOut, Menu, UserRound, X } from "lucide-react";
import type { UserProfileDto } from "@/types/api";
import { useLogout } from "@/features/auth/useLogout";
import { ApiError } from "@/lib/ApiError";

type AppLayoutProps = {
  children: ReactNode;
  user: UserProfileDto;
};

export function AppLayout({ children, user }: AppLayoutProps) {
  const [navigationOpen, setNavigationOpen] = useState<boolean>(false);
  const navigationId = useId();
  const logoutMutation = useLogout();

  return (
    <div className="min-h-screen bg-background lg:grid lg:grid-cols-[16rem_minmax(0,1fr)]">
      <a
        href="#main-content"
        className="sr-only z-50 rounded-md bg-background px-4 py-2 text-foreground focus:not-sr-only focus:fixed focus:left-4 focus:top-4"
      >
        Skip to main content
      </a>

      <aside className="border-b bg-sidebar text-sidebar-foreground lg:sticky lg:top-0 lg:h-screen lg:border-b-0 lg:border-r">
        <div className="flex items-center justify-between gap-4 px-6 py-5">
          <Link
            to="/"
            className="flex min-w-0 items-center gap-3 rounded-lg focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring"
          >
            <span className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-primary text-primary-foreground shadow-sm">
              <Boxes aria-hidden="true" className="size-6" strokeWidth={1.75} />
            </span>

            <span className="min-w-0">
              <span className="block text-xl font-semibold tracking-tight">
                StockFlow
              </span>
              <span className="mt-0.5 block text-xs text-muted-foreground">
                NovaTrade operations
              </span>
            </span>
          </Link>

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
        <header className="flex min-h-20 items-center justify-between gap-4 border-b bg-card px-4 py-3 sm:px-6 lg:px-8">
          <div className="hidden sm:block">
            <p className="text-sm font-semibold">Operations workspace</p>
            <p className="mt-0.5 text-xs text-muted-foreground">
              Inventory and supplier management
            </p>
          </div>

          <div className="ml-auto flex min-w-0 items-center gap-3">
            <span className="hidden size-10 shrink-0 items-center justify-center rounded-full border bg-muted text-muted-foreground sm:flex">
              <UserRound aria-hidden="true" className="size-5" />
            </span>

            <div className="min-w-0 text-right sm:text-left">
              <p
                className="max-w-40 truncate text-sm font-semibold lg:max-w-56"
                title={user.displayName}
              >
                {user.displayName}
              </p>

              <p
                className="max-w-40 truncate text-xs text-muted-foreground lg:max-w-56"
                title={user.roles.join(", ")}
              >
                {user.roles.join(", ")}
              </p>
            </div>

            <div className="ml-1 border-l pl-3">
              <Button
                type="button"
                variant="ghost"
                className="min-h-11 shrink-0 gap-2 text-muted-foreground hover:text-foreground"
                disabled={logoutMutation.isPending}
                onClick={() => {
                  if (!logoutMutation.isPending) {
                    logoutMutation.mutate();
                  }
                }}
              >
                <LogOut aria-hidden="true" className="size-4" />
                {logoutMutation.isPending ? "Signing out…" : "Sign out"}
              </Button>
            </div>
          </div>
        </header>

        {logoutMutation.isError && (
          <p
            role="alert"
            className="px-4 py-3 text-sm text-destructive sm:px-6 lg:px-8"
          >
            {logoutMutation.error instanceof ApiError
              ? logoutMutation.error.message
              : "Couldn't confirm logout. Check your connection and try again."}
          </p>
        )}

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
