import { Navigate, Outlet } from "react-router";
import { AppLayout } from "@/components/layout/AppLayout";
import { Button } from "@/components/ui/button";
import { useCurrentUser } from "@/features/auth/useCurrentUser";
import { ApiError } from "@/lib/ApiError";

export function ProtectedLayout() {
  const query = useCurrentUser();

  if (query.isPending) {
    return (
      <main className="flex min-h-dvh items-center justify-center bg-background px-4 py-10">
        <div
          role="status"
          className="w-full max-w-md rounded-xl border bg-card p-8 text-center shadow-sm"
        >
          <p className="font-semibold">Opening your workspace</p>
          <p className="mt-2 text-sm text-muted-foreground">
            Checking your session…
          </p>
        </div>
      </main>
    );
  }

  if (query.isError) {
    return (
      <main className="flex min-h-dvh items-center justify-center bg-background px-4 py-10">
        <div className="w-full max-w-md space-y-5 rounded-xl border bg-card p-8 shadow-sm">
          <div role="alert" className="space-y-2">
            <h1 className="text-lg font-semibold">
              Couldn't open your workspace
            </h1>

            <p className="text-sm leading-6 text-muted-foreground">
              {query.error instanceof ApiError
                ? query.error.message
                : "Couldn't check your session. Check your connection and try again."}
            </p>
          </div>

          <Button
            type="button"
            variant="outline"
            className="min-h-11 w-full"
            disabled={query.isFetching}
            onClick={() => void query.refetch()}
          >
            {query.isFetching ? "Checking session…" : "Try again"}
          </Button>
        </div>
      </main>
    );
  }

  if (query.data === null) {
    return <Navigate to="/login" replace />;
  }

  return (
    <AppLayout user={query.data}>
      <Outlet context={{ user: query.data }} />
    </AppLayout>
  );
}
