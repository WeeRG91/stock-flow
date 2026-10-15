import { Link } from "react-router";
import { PageHeader } from "@/components/PageHeader";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";

export function DashboardPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Dashboard"
        description="An overview of NovaTrade's inventory operations."
      />

      <Card>
        <CardHeader>
          <CardTitle>
            <h2>Inventory overview</h2>
          </CardTitle>
          <CardDescription>
            Stock reporting will appear here when data access is connected.
          </CardDescription>
        </CardHeader>

        <CardContent>
          <Link
            to="/inventory"
            className="text-sm font-medium underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-4"
          >
            Open inventory
          </Link>
        </CardContent>
      </Card>
    </div>
  );
}