import type { UpdateSupplierRequest } from "@/types/api";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { updateSupplier } from "./api";

export function useUpdateSupplier(supplierId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: UpdateSupplierRequest) =>
      updateSupplier(supplierId, request),
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
