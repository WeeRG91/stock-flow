import { z } from "zod";

const lineSchema = z.object({
  productId: z
    .string()
    .regex(
      /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i,
      "Select a valid product.",
    )
    .refine(
      (value) => value !== "00000000-0000-0000-0000-000000000000",
      { message: "Select a valid product." },
    ),
  quantity: z
    .number({ error: "Enter a quantity." })
    .int("Quantity must be a whole number.")
    .positive("Quantity must be greater than zero.")
    .max(2_147_483_647, "Quantity exceeds the supported limit."),
});

export const updateDraftTransferSchema = z
  .object({
    lines: z.array(lineSchema),
  })
  .superRefine(({ lines }, context) => {
    const seenProducts = new Set<string>();

    lines.forEach(({ productId }, index) => {
      const key = productId.toLowerCase();

      if (seenProducts.has(key)) {
        context.addIssue({
          code: "custom",
          path: ["lines", index, "productId"],
          message: "This product is already on the transfer.",
        });
      }

      seenProducts.add(key);
    });
  });

export type UpdateDraftTransferFormValues =
  z.infer<typeof updateDraftTransferSchema>;