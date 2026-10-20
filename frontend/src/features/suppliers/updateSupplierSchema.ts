import { z } from "zod";
import { createSupplierSchema } from "./createSupplierSchema";

export const updateSupplierSchema = createSupplierSchema.omit({
  code: true,
});

export type UpdateSupplierFormValues = z.infer<typeof updateSupplierSchema>;
