import { useState } from "react";
import { Button } from "@/components/ui/button";
import { FeedbackPanel } from "@/components/FeedbackPanel";
import { useProducts } from "@/features/products/useProducts";
import type { TransferDetailsDto } from "@/types/api";
import { EditDraftTransferForm } from "./EditDraftTransferForm";

export function EditDraftTransferSection({
  transfer,
}: {
  transfer: TransferDetailsDto;
}) {
  const [editing, setEditing] = useState(false);
  const products = useProducts();

  if (!editing) {
    return (
      <Button
        type="button"
        variant="outline"
        className="min-h-11"
        onClick={() => setEditing(true)}
      >
        Edit product lines
      </Button>
    );
  }

  if (products.isPending) {
    return (
      <FeedbackPanel title="Loading product options" role="status">
        <p>Fetching available products.</p>
      </FeedbackPanel>
    );
  }

  if (products.isError) {
    return (
      <FeedbackPanel title="Couldn't load product options" role="alert">
        <p>{products.error.message}</p>
        <Button
          type="button"
          variant="outline"
          onClick={() => setEditing(false)}
        >
          Close editor
        </Button>
      </FeedbackPanel>
    );
  }

  return (
    <EditDraftTransferForm
      transfer={transfer}
      products={products.data}
      onSaved={() => setEditing(false)}
      onCancel={() => setEditing(false)}
    />
  );
}