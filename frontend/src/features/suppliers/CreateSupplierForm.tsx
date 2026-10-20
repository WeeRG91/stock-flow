import { useForm } from "react-hook-form";
import {
  createSupplierSchema,
  type CreateSupplierFormValues,
} from "./createSupplierSchema";
import { zodResolver } from "@hookform/resolvers/zod";
import { useCreateSupplier } from "./useCreateSupplier";
import { ApiError } from "@/lib/ApiError";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";

const fields = [
  { name: "code", serverName: "Code", label: "Code", type: "text" },
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
    label: "Phone Number (optional)",
    type: "tel",
  },
] as const;

export function CreateSupplierForm() {
  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors },
  } = useForm<CreateSupplierFormValues>({
    resolver: zodResolver(createSupplierSchema),
    mode: "onBlur",
    defaultValues: {
      code: "",
      name: "",
      contactEmail: "",
      phoneNumber: "",
    },
  });

  const mutation = useCreateSupplier();

  function onSubmit(values: CreateSupplierFormValues) {
    if (mutation.isPending) return;

    mutation.mutate(
      {
        ...values,
        phoneNumber: values.phoneNumber || null,
      },
      {
        onSuccess: () => reset(),
        onError: (error) => {
          if (!(error instanceof ApiError)) return;

          if (error.status === 409) {
            setError(
              "code",
              {
                type: "server",
                message: "This supplier code is already in use.",
              },
              { shouldFocus: true },
            );
            return;
          }

          if (error.status !== 400) return;

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
      aria-labelledby="create-supplier-heading"
      className="space-y-4 rounded-xl border bg-card p-6"
      noValidate
      onSubmit={handleSubmit(onSubmit)}
    >
      <h2 id="create-supplier-heading" className="text-lg font-semibold">
        Create supplier
      </h2>

      <fieldset
        disabled={mutation.isPending}
        className="grid min-w-0 gap-4 sm:grid-cols-2"
      >
        {fields.map((field) => {
          const id = `create-supplier-${field.name}`;
          const error = errors[field.name];

          return (
            <div key={field.name} className="space-y-2">
              <Label htmlFor={id}>{field.label}</Label>
              <Input
                id={id}
                type={field.type}
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

      <Button type="submit" disabled={mutation.isPending}>
        {mutation.isPending ? "Creating supplier…" : "Create supplier"}
      </Button>

      {mutation.isSuccess && (
        <p role="status" className="text-sm">
          Supplier {mutation.data.code} created successfully.
        </p>
      )}

      {mutation.isError && (
        <p role="alert" className="text-sm text-destructive">
          {mutation.error instanceof ApiError
            ? mutation.error.message
            : "Couldn't confirm creation. Check the supplier list before submitting again."}
        </p>
      )}
    </form>
  );
}
