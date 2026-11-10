import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ApiError } from "@/lib/ApiError";
import type { UserProfileDto } from "@/types/api";
import { logout } from "./api";

export function useLogout() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async () => {
      try {
        await logout();
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          return;
        }

        throw error;
      }
    },
    retry: false,
    gcTime: 0,
    onSuccess: async () => {
      await queryClient.cancelQueries();

      queryClient.removeQueries({
        predicate: (query) => query.queryKey[0] !== "auth",
      });

      queryClient.setQueryData<UserProfileDto | null>(
        ["auth", "me"],
        null,
      );

      queryClient.getMutationCache().clear();
    },
  });
}