import { useMutation, useQueryClient } from "@tanstack/react-query";
import { login } from "./api";

export function useLogin() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: login,
    retry: false,
    gcTime: 0,
    onSuccess: async () => {
      await queryClient.cancelQueries();
      queryClient.removeQueries();
    },
  });
}