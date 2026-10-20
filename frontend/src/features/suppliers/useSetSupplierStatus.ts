import { useMutation, useQueryClient } from "@tanstack/react-query";
import { activateSupplier, deactivateSupplier } from "./api";

export function useSetSupplierStatus(supplierId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (isActive: boolean) =>
      isActive ? activateSupplier(supplierId) : deactivateSupplier(supplierId),
    retry: false,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: ["suppliers", "list"],
        }),
        queryClient.invalidateQueries({
          queryKey: ["suppliers", "detail", supplierId],
        }),
      ]);
    },
  });
}
