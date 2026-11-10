import { StatusBadge } from "@/components/StatusBadge";
import { Card, CardContent } from "@/components/ui/card";
import type { SupplierDto } from "@/types/api";
import { Link } from "react-router";

type SuppliersTableProps = {
  suppliers: SupplierDto[];
};

export function SuppliersTable({ suppliers }: SuppliersTableProps) {
  return (
    <Card className="gap-0 py-0">
      <div className="border-b px-6 py-5">
        <h2 className="text-base font-semibold">Supplier directory</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Supplier contact information and account status.
        </p>
      </div>

      <CardContent className="px-0">
        <div className="overflow-x-auto">
          <table className="w-full min-w-180 text-left text-sm">
            <caption className="sr-only">Supplier directory</caption>

            <thead className="border-b bg-muted/50 text-xs text-muted-foreground">
              <tr>
                <th scope="col" className="px-6 py-3 font-medium">
                  Code
                </th>
                <th scope="col" className="px-6 py-3 font-medium">
                  Name
                </th>
                <th scope="col" className="px-6 py-3 font-medium">
                  Email
                </th>
                <th scope="col" className="px-6 py-3 font-medium">
                  Phone
                </th>
                <th scope="col" className="px-6 py-3 font-medium">
                  Status
                </th>
              </tr>
            </thead>

            <tbody className="divide-y divide-border">
              {suppliers.map((supplier) => (
                <tr key={supplier.id} className="hover:bg-muted/30">
                  <td className="whitespace-nowrap px-6 py-4">
                    <Link
                      to={`/suppliers/${supplier.id}`}
                      className="rounded-sm font-mono text-xs font-semibold text-primary underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-ring"
                    >
                      {supplier.code}
                    </Link>
                  </td>

                  <td className="px-6 py-4 font-medium">
                    {supplier.name}
                  </td>

                  <td className="px-6 py-4 text-muted-foreground wrap-break-word">
                    {supplier.contactEmail}
                  </td>

                  <td className="whitespace-nowrap px-6 py-4 text-muted-foreground">
                    {supplier.phoneNumber ?? "Not provided"}
                  </td>

                  <td className="px-6 py-4">
                    <StatusBadge isActive={supplier.isActive} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </CardContent>
    </Card>
  );
}