import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/ApiError";
import type { SupplierDto } from "@/types/api";
import {
  updateSupplierSchema,
  type UpdateSupplierFormValues,
} from "./updateSupplierSchema";
import { useUpdateSupplier } from "./useUpdateSupplier";

type UpdateSupplierFormProps = {
  supplier: SupplierDto;
};

const fields = [
  { name: "name", serverName: "Name", label: "Name", type: "text" },
  {
    name: "contactEmail",
    serverName: "ContactEmail",
    label: "Contact email",
    type: "email",
  },
  {
    name: "phoneNumber",
    serverName: "PhoneNumber",
    label: "Phone number (optional)",
    type: "tel",
  },
] as const;

export function UpdateSupplierForm({ supplier }: UpdateSupplierFormProps) {
  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors },
  } = useForm<UpdateSupplierFormValues>({
    resolver: zodResolver(updateSupplierSchema),
    mode: "onBlur",
    defaultValues: {
      name: supplier.name,
      contactEmail: supplier.contactEmail,
      phoneNumber: supplier.phoneNumber ?? "",
    },
  });

  const mutation = useUpdateSupplier(supplier.id);

  function onSubmit(values: UpdateSupplierFormValues) {
    if (mutation.isPending) return;

    mutation.mutate(
      {
        ...values,
        phoneNumber: values.phoneNumber || null,
      },
      {
        onSuccess: (updated) => {
          reset({
            name: updated.name,
            contactEmail: updated.contactEmail,
            phoneNumber: updated.phoneNumber ?? "",
          });
        },
        onError: (error) => {
          if (!(error instanceof ApiError) || error.status !== 400) {
            return;
          }

          const serverErrors = error.problem?.errors;
          if (!serverErrors) return;

          for (const field of fields) {
            const messages =
              serverErrors[field.name] ?? serverErrors[field.serverName];

            if (messages?.length) {
              setError(field.name, {
                type: "server",
                message: messages.join(" "),
              });
            }
          }
        },
      },
    );
  }

  return (
    <form
      aria-labelledby="update-supplier-heading"
      className="space-y-6 rounded-xl border border-border bg-card p-6 shadow-sm"
      noValidate
      onSubmit={handleSubmit(onSubmit)}
    >
      <div className="space-y-1">
        <h2 id="update-supplier-heading" className="text-base font-semibold">
          Edit contact details
        </h2>
        <p className="text-sm leading-6 text-muted-foreground">
          Update the supplier’s name and contact information. The supplier code
          stays unchanged.
        </p>
      </div>

      <fieldset
        disabled={mutation.isPending}
        className="grid min-w-0 items-start gap-5 sm:grid-cols-2"
      >
        {fields.map((field) => {
          const id = `update-supplier-${field.name}`;
          const error = errors[field.name];

          return (
            <div key={field.name} className="min-w-0 space-y-2">
              <Label htmlFor={id}>{field.label}</Label>
              <Input
                id={id}
                type={field.type}
                className="h-11 bg-card"
                aria-invalid={Boolean(error)}
                aria-describedby={error ? `${id}-error` : undefined}
                {...register(field.name)}
              />

              {error && (
                <p id={`${id}-error`} className="text-sm text-destructive">
                  {error.message}
                </p>
              )}
            </div>
          );
        })}
      </fieldset>

      <Button
        type="submit"
        className="min-h-11 w-full sm:w-auto"
        disabled={mutation.isPending}
      >
        {mutation.isPending ? "Saving changes…" : "Save changes"}
      </Button>

      {mutation.isSuccess && (
        <p
          role="status"
          className="rounded-lg border border-primary/20 bg-accent px-4 py-3 text-sm text-accent-foreground"
        >
          Supplier details saved successfully.
        </p>
      )}

      {mutation.isError && (
        <p
          role="alert"
          className="rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive"
        >
          {mutation.error instanceof ApiError
            ? mutation.error.message
            : "Couldn't confirm the update. Check supplier details before submitting again."}
        </p>
      )}
    </form>
  );
}
