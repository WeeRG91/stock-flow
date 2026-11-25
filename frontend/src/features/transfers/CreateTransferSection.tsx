import { FeedbackPanel } from "@/components/FeedbackPanel";
import { useWarehouses } from "@/features/warehouses/useWarehouses";
import { CreateTransferForm } from "./CreateTransferForm";

export function CreateTransferSection() {
  const warehouses = useWarehouses();

  if (warehouses.isPending) {
    return (
      <FeedbackPanel title="Loading transfer options" role="status">
        <p>Fetching available warehouses.</p>
      </FeedbackPanel>
    );
  }

  if (warehouses.isError) {
    return (
      <FeedbackPanel title="Couldn't load transfer options" role="alert">
        <p>{warehouses.error.message}</p>
      </FeedbackPanel>
    );
  }

  const activeWarehouses = warehouses.data.filter(
    (warehouse) => warehouse.isActive,
  );

  if (activeWarehouses.length < 2) {
    return (
      <FeedbackPanel title="Two active warehouses are required">
        <p>A transfer needs different source and destination warehouses.</p>
      </FeedbackPanel>
    );
  }

  return <CreateTransferForm warehouses={activeWarehouses} />;
}
