type StatusBadgeProps = {
  isActive: boolean;
};

export function StatusBadge({ isActive }: StatusBadgeProps) {
  return (
    <span
      className={[
        "inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-2.5 py-1 text-xs font-medium",
        isActive
          ? "bg-accent text-accent-foreground"
          : "bg-muted text-muted-foreground",
      ].join(" ")}
    >
      <span
        aria-hidden="true"
        className="size-1.5 rounded-full bg-current"
      />

      {isActive ? "Active" : "Inactive"}
    </span>
  );
}