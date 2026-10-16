import { z } from "zod";

const emptyGuid = "00000000-0000-0000-0000-000000000000";

const identifierSchema = z
  .string()
  .regex(
    /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i,
    "Select a valid option.",
  )
  .refine((value) => value !== emptyGuid, {
    message: "Select a valid option.",
  });

export const receiveStockSchema = z.object({
  productId: identifierSchema,
  warehouseId: identifierSchema,
  quantity: z
    .number({ error: "Enter a valid quantity." })
    .int("Quantity must be a whole number.")
    .positive("Quantity must be greater than zero.")
    .max(2_147_483_647, "Quantity exceeds the supported limit."),
});

export type ReceiveStockFormValues = z.infer<typeof receiveStockSchema>;
