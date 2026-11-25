import { z } from "zod";

const identifierSchema = z
  .string()
  .regex(
    /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i,
    "Select a valid option.",
  )
  .refine((value) => value !== "00000000-0000-0000-0000-000000000000", {
    message: "Select a valid option.",
  });

export const createTransferSchema = z
  .object({
    sourceWarehouseId: identifierSchema,
    destinationWarehouseId: identifierSchema,
    lines: z.array(z.never()).length(0),
  })
  .superRefine((values, context) => {
    if (
      values.sourceWarehouseId.toLowerCase() ===
      values.destinationWarehouseId.toLowerCase()
    ) {
      context.addIssue({
        code: "custom",
        path: ["destinationWarehouseId"],
        message: "Choose a different destination warehouse.",
      });
    }
  });

export type CreateTransferFormValues = z.infer<typeof createTransferSchema>;
