import { ArrowLeft, FileQuestion } from "lucide-react";
import { Link } from "react-router";

export function NotFoundPage() {
  return (
    <section
      aria-labelledby="not-found-heading"
      className="rounded-xl border border-border bg-card px-6 py-16 text-center shadow-sm"
    >
      <span className="mx-auto flex size-14 items-center justify-center rounded-xl bg-muted text-muted-foreground">
        <FileQuestion aria-hidden="true" className="size-7" />
      </span>

      <p className="mt-6 text-sm font-semibold text-primary">404</p>

      <h1
        id="not-found-heading"
        className="mt-2 text-2xl font-semibold tracking-tight"
      >
        Page not found
      </h1>

      <p className="mx-auto mt-3 max-w-sm text-sm leading-6 text-muted-foreground">
        The page you requested doesn't exist. Return to the dashboard
        to continue exploring your workspace.
      </p>

      <Link
        to="/"
        className="mt-6 inline-flex min-h-11 items-center justify-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90 focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring"
      >
        <ArrowLeft aria-hidden="true" className="size-4" />
        Return to dashboard
      </Link>
    </section>
  );
}