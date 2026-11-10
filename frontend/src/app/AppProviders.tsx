import { ApiError } from "@/lib/ApiError";
import {
  MutationCache,
  QueryCache,
  QueryClient,
  QueryClientProvider,
} from "@tanstack/react-query";
import { useState, type ReactNode } from "react";

type AppProvidersProps = {
  children: ReactNode;
};

export function AppProviders({ children }: AppProvidersProps) {
  const [queryClient] = useState(() => {
    function checkSessionOnUnauthorized(error: unknown) {
      if (error instanceof ApiError && error.status === 401) {
        void client.invalidateQueries(
          {
            queryKey: ["auth", "me"],
            exact: true,
          },
          {
            cancelRefetch: false,
          },
        );
      }
    }

    const client: QueryClient = new QueryClient({
      queryCache: new QueryCache({
        onError: checkSessionOnUnauthorized,
      }),
      mutationCache: new MutationCache({
        onError: checkSessionOnUnauthorized,
      }),
      defaultOptions: {
        queries: {
          retry: (failureCount, error) => {
            if (
              error instanceof ApiError &&
              (error.status === 401 || error.status === 403)
            ) {
              return false;
            }
            return failureCount < 3;
          },
        },
      },
    });

    return client;
  });

  return (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}
