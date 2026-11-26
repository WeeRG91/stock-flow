import type { StockReservationDto } from "@/types/api";

type Props = {
  reservation: StockReservationDto;
};

const dateFormatter = new Intl.DateTimeFormat(undefined, {
  dateStyle: "medium",
  timeStyle: "short",
});

export function StockReservationHistory({ reservation }: Props) {
  const events = [
    {
      label: "Created",
      at: reservation.createdAt,
      actorId: reservation.createdByUserId,
    },
    {
      label: "Released",
      at: reservation.releasedAt,
      actorId: reservation.releasedByUserId,
    },
    {
      label: "Fulfilled",
      at: reservation.fulfilledAt,
      actorId: reservation.fulfilledByUserId,
    },
  ];

  return (
    <section
      aria-label="Reservation history"
      className="space-y-4 border-t pt-6"
    >
      <h3 className="font-semibold">History</h3>

      <ol className="space-y-4">
        {events.map((event) =>
          event.at === null ? null : (
            <li key={event.label} className="space-y-1 text-sm">
              <p className="font-medium">{event.label}</p>

              <time dateTime={event.at} className="text-muted-foreground">
                {dateFormatter.format(new Date(event.at))}
              </time>

              {event.actorId && (
                <p className="text-xs text-muted-foreground wrap-break-word">
                  Actor ID: {event.actorId}
                </p>
              )}
            </li>
          ),
        )}
      </ol>
    </section>
  );
}
