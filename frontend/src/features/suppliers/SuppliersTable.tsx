import { Card, CardContent } from "@/components/ui/card";
import type { SupplierDto } from "@/types/api";
import { Link } from "react-router";

type SuppliersTableProps = {
  suppliers: SupplierDto[];
};

export function SuppliersTable({ suppliers }: SuppliersTableProps) {
  return (
    <Card>
      <CardContent>
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <caption className="sr-only">Supplier directory</caption>

            <thead>
              <tr className="border-b">
                <th scope="col" className="px-3 py-3">
                  Code
                </th>
                <th scope="col" className="px-3 py-3">
                  Name
                </th>
                <th scope="col" className="px-3 py-3">
                  Email
                </th>
                <th scope="col" className="px-3 py-3">
                  Phone
                </th>
                <th scope="col" className="px-3 py-3">
                  Status
                </th>
              </tr>
            </thead>

            <tbody>
              {suppliers.map((supplier) => (
                <tr key={supplier.id} className="border-b last:border-0">
                  <td className="whitespace-nowrap px-3 py-3 font-medium">
                    <Link
                      to={`/suppliers/${supplier.id}`}
                      className="underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-2"
                    >
                      {supplier.code}
                    </Link>
                  </td>
                  <td className="px-3 py-3">{supplier.name}</td>
                  <td className="px-3 py-3">{supplier.contactEmail}</td>
                  <td className="px-3 py-3 text-muted-foreground">
                    {supplier.phoneNumber ?? "Not provided"}
                  </td>
                  <td className="px-3 py-3">
                    {supplier.isActive ? "Active" : "Inactive"}
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
