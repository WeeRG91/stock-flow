import { PageHeader } from "@/components/PageHeader";
import { Link } from "react-router";

export function NotFoundPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Page not found"
        description="The page you requested doesn't exist."
      />

      <Link
        to="/"
        className="text-sm font-medium underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-4"
      >
        Return to dashboard
      </Link>
    </div>
  );
}
