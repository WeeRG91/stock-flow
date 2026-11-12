import { z } from "zod";

const emptyGuid = "00000000-0000-0000-0000-000000000000";

const selectionSchema = z
  .string()
  .regex(
    /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i,
    "Select a valid option.",
  )
  .refine((value) => value !== emptyGuid, {
    message: "Select a valid option.",
  });

export const createPurchaseOrderSchema = z.object({
  supplierId: selectionSchema,
  warehouseId: selectionSchema,
});

export type CreatePurchaseOrderFormValues = z.infer<
  typeof createPurchaseOrderSchema
>;
