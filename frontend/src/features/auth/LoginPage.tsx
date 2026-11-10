import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useNavigate } from "react-router";
import { PageHeader } from "@/components/PageHeader";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/ApiError";
import { loginSchema, type LoginFormValues } from "./loginSchema";
import { useLogin } from "./useLogin";
import { Boxes } from "lucide-react";

export function LoginPage() {
  const navigate = useNavigate();
  const mutation = useLogin();

  const {
    register,
    handleSubmit,
    resetField,
    setError,
    formState: { errors },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    mode: "onBlur",
    defaultValues: {
      email: "",
      password: "",
    },
  });

  function onSubmit(values: LoginFormValues) {
    if (mutation.isPending) return;

    mutation.mutate(values, {
      onSuccess: () => {
        resetField("password");
        navigate("/", { replace: true });
      },
      onError: (error) => {
        resetField("password");

        if (!(error instanceof ApiError) || error.status !== 400) {
          return;
        }

        const serverErrors = error.problem?.errors;
        if (!serverErrors) return;

        const fields = [
          ["email", "Email"],
          ["password", "Password"],
        ] as const;

        for (const [field, serverField] of fields) {
          const messages = serverErrors[field] ?? serverErrors[serverField];

          if (messages?.length) {
            setError(field, {
              type: "server",
              message: messages.join(" "),
            });
          }
        }
      },
    });
  }

  return (
    <main className="flex min-h-dvh items-center justify-center bg-background px-4 py-10 sm:px-6">
      <div className="w-full max-w-md space-y-8">
        <div className="flex items-center gap-3">
          <span className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-primary text-primary-foreground shadow-sm">
            <Boxes aria-hidden="true" className="size-7" strokeWidth={1.75} />
          </span>

          <div>
            <p className="text-xl font-semibold tracking-tight">StockFlow</p>
            <p className="mt-0.5 text-xs text-muted-foreground">
              NovaTrade operations
            </p>
          </div>
        </div>

        <PageHeader
          title="Sign in to StockFlow"
          description="Use your NovaTrade account to continue."
        />

        <form
          className="space-y-6 rounded-xl border border-border bg-card p-6 shadow-sm sm:p-8"
          noValidate
          onSubmit={handleSubmit(onSubmit)}
        >
          <fieldset disabled={mutation.isPending} className="min-w-0 space-y-5">
            <div className="space-y-2">
              <Label htmlFor="login-email">Email</Label>
              <Input
                id="login-email"
                type="email"
                className="h-11 bg-card"
                autoComplete="username"
                aria-invalid={Boolean(errors.email)}
                aria-describedby={
                  errors.email ? "login-email-error" : undefined
                }
                {...register("email")}
              />

              {errors.email && (
                <p id="login-email-error" className="text-sm text-destructive">
                  {errors.email.message}
                </p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="login-password">Password</Label>
              <Input
                id="login-password"
                type="password"
                className="h-11 bg-card"
                autoComplete="current-password"
                aria-invalid={Boolean(errors.password)}
                aria-describedby={
                  errors.password ? "login-password-error" : undefined
                }
                {...register("password")}
              />

              {errors.password && (
                <p
                  id="login-password-error"
                  className="text-sm text-destructive"
                >
                  {errors.password.message}
                </p>
              )}
            </div>
          </fieldset>

          <Button
            type="submit"
            className="min-h-11 w-full"
            disabled={mutation.isPending}
          >
            {mutation.isPending ? "Signing in…" : "Sign in"}
          </Button>

          {mutation.isPending && (
            <p role="status" className="text-center text-sm text-muted-foreground">
              Checking your credentials…
            </p>
          )}

          {mutation.isError && (
            <p role="alert" className="rounded-lg border border-destructive/20 bg-destructive/5 px-4 py-3 text-sm text-destructive">
              {mutation.error instanceof ApiError
                ? mutation.error.message
                : "Couldn't sign in. Check your connection and try again."}
            </p>
          )}
        </form>
      </div>
    </main>
  );
}
