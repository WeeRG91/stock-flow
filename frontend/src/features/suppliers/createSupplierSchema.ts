import { z } from "zod";

export const createSupplierSchema = z.object({
  code: z
    .string()
    .trim()
    .min(1, "Supplier code is required.")
    .max(64, "Supplier code must not exceed 64 characters."),

  name: z
    .string()
    .trim()
    .min(1, "Supplier name is required.")
    .max(200, "Supplier name must not exceed 200 characters."),

  contactEmail: z
    .string()
    .trim()
    .min(1, "Contact email is required.")
    .max(254, "Contact email must not exceed 254 characters."),

  phoneNumber: z
    .string()
    .trim()
    .max(50, "Phone number must not exceed 50 characters."),
});

export type CreateSupplierFormValues = z.infer<typeof createSupplierSchema>;
