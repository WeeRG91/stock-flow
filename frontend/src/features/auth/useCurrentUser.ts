import { useQuery, useQueryClient } from "@tanstack/react-query";
import { getCurrentUser } from "./api";

export function useCurrentUser() {
  const queryClient = useQueryClient();

  return useQuery({
    queryKey: ["auth", "me"],
    queryFn: async ({ signal }) => {
      const user = await getCurrentUser(signal);

      signal.throwIfAborted();

      if (user === null) {
        const businessQueries = {
          predicate: (query: { queryKey: readonly unknown[] }) =>
            query.queryKey[0] !== "auth",
        };

        await queryClient.cancelQueries(businessQueries);

        signal.throwIfAborted();

        queryClient.removeQueries(businessQueries);
        queryClient.getMutationCache().clear();
      }

      return user;
    },
    staleTime: 0,
    retry: false,
    refetchOnWindowFocus: true,
    refetchInterval: 60_000,
    refetchIntervalInBackground: false,
  });
}
