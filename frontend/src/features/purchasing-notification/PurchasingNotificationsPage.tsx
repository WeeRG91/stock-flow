import type { PurchasingNotificationKind } from "@/types/api";
import { usePermissions } from "../auth/usePermissions";
import { useState } from "react";
import { usePurchasingNotifications } from "./usePurchasingNotifications";
import { useMarkPurchasingNotificationAsRead } from "./useMarkPurchasingNotificationAsRead";
import { PageHeader } from "@/components/PageHeader";
import { FeedbackPanel } from "@/components/FeedbackPanel";
import { Button } from "@/components/ui/button";
import { Link } from "react-router";

const notificationLabels: Record<PurchasingNotificationKind, string> = {
  1: "Purchase order submitted for approval",
  2: "Purchase order approved",
  3: "Purchase order rejected",
};

const dateFormat = new Intl.DateTimeFormat(undefined, {
  dateStyle: "medium",
  timeStyle: "short",
});

export function PurchasingNotificationsPage() {
  const { user } = usePermissions();
  const [pageNumber, setPageNumber] = useState<number>(1);
  const query = usePurchasingNotifications(user.id, pageNumber);
  const markAsRead = useMarkPurchasingNotificationAsRead(user.id);
  const inbox = query.data;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Purchasing notifications"
        description="Review purchase order submissions and decisions."
      />

      {query.isPending && <p role="status">Loading notifications…</p>}
      {query.isError && (
        <FeedbackPanel title="Couldn't load notifications" role="alert">
          <Button
            type="button"
            variant="outline"
            disabled={query.isFetching}
            onClick={() => void query.refetch()}
          >
            Try again
          </Button>
        </FeedbackPanel>
      )}

      {markAsRead.isError && (
        <p role="alert" className="text-sm text-destructive">
          {markAsRead.error.message}
        </p>
      )}

      {inbox && (
        <>
          <p role="status" className="text-sm text-muted-foreground">
            {inbox.unreadCount} unread
          </p>

          {inbox.items.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              {inbox.totalCount === 0
                ? "You have no purchasing notifications."
                : "There are no notifications on this page."}
            </p>
          ) : (
            <ul className="divide-y rounded-xl border bg-card">
              {inbox.items.map((item) => (
                <li
                  key={item.id}
                  className="flex flex-wrap items-start gap-4 p-4"
                >
                  <div className="min-w-0 flex-1 space-y-1">
                    <p className="font-medium">
                      {notificationLabels[item.kind]}
                    </p>
                    <p className="text-sm text-muted-foreground">
                      <time dateTime={item.createdAt}>
                        {dateFormat.format(new Date(item.createdAt))}
                      </time>
                      {item.readAt === null && " · Unread"}
                    </p>
                    <Link
                      to={`/purchase-orders/${item.purchaseOrderId}`}
                      className="text-sm text-primary underline underline-offset-4"
                      onClick={() => {
                        if (item.readAt === null) {
                          markAsRead.mutate(item.id);
                        }
                      }}
                    >
                      Open purchase order
                    </Link>
                  </div>

                  {item.readAt === null && (
                    <Button
                      type="button"
                      variant="outline"
                      disabled={markAsRead.isPending}
                      onClick={() => markAsRead.mutate(item.id)}
                    >
                      {markAsRead.isPending && markAsRead.variables === item.id
                        ? "Marking as read…"
                        : "Mark as read"}
                    </Button>
                  )}
                </li>
              ))}
            </ul>
          )}
        </>
      )}

      {(inbox || pageNumber > 1) && (
        <nav
          aria-label="Notification pages"
          className="flex flex-wrap items-center gap-3"
        >
          <Button
            type="button"
            variant="outline"
            disabled={pageNumber <= 1 || query.isFetching}
            onClick={() => setPageNumber((page) => page - 1)}
          >
            Previous
          </Button>
          <span className="text-sm">Page {pageNumber}</span>
          <Button
            type="button"
            variant="outline"
            disabled={
              !inbox ||
              query.isFetching ||
              inbox.pageNumber * inbox.pageSize >= inbox.totalCount
            }
            onClick={() => setPageNumber((page) => page + 1)}
          >
            Next
          </Button>
        </nav>
      )}
    </div>
  );
}
