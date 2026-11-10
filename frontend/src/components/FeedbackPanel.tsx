import type { ReactNode } from "react";

type FeedbackPanelProps = {
  title: string;
  children: ReactNode;
  role?: "status" | "alert";
};

export function FeedbackPanel({
  title,
  children,
  role,
}: FeedbackPanelProps) {
  return (
    <div
      role={role}
      className="space-y-3 rounded-xl border border-border bg-card p-6 shadow-sm"
    >
      <p className="font-semibold text-foreground">{title}</p>
      <div className="space-y-3 text-sm leading-6 text-muted-foreground">
        {children}
      </div>
    </div>
  );
}