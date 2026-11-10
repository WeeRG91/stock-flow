import { z } from "zod";

export const loginSchema = z.object({
  email: z
  .string()
  .trim()
  .min(1, "Email is required.")
  .max(256, "Email must not exceed 256 characters.")
  .pipe(z.email({ error: "Enter a valid email address." })),

  password: z
    .string()
    .min(1, "Password is required."),
});

export type LoginFormValues = z.infer<typeof loginSchema>;