import type { ReceiveTransferLineRequest, TransferLineDto } from "@/types/api";

type ReceiptValidationResult =
  | { valid: true; lines: ReceiveTransferLineRequest[] }
  | { valid: false; message: string; lineId?: string };

export function validateTransferReceipt(
  transferLines: readonly TransferLineDto[],
  quantities: Record<string, string>,
): ReceiptValidationResult {
  const lines: ReceiveTransferLineRequest[] = [];

  for (const line of transferLines) {
    if (line.inTransitQuantity <= 0) continue;

    const entered = quantities[line.id]?.trim();
    if (!entered) continue;

    const quantity = Number(entered);

    if (
      !Number.isInteger(quantity) ||
      quantity < 1 ||
      quantity > line.inTransitQuantity
    ) {
      return {
        valid: false,
        lineId: line.id,
        message:
          `Enter a whole number from 1 to ${line.inTransitQuantity} ` +
          `for ${line.productName}.`,
      };
    }

    lines.push({
      transferLineId: line.id,
      receivedQuantity: quantity,
    });
  }

  if (lines.length === 0) {
    return {
      valid: false,
      message: "Enter a received quantity for at least one product.",
    };
  }

  return { valid: true, lines };
}
