export function relativeTime(isoString: string): string {
  const now = Date.now();
  const then = new Date(isoString).getTime();
  const diff = Math.floor((now - then) / 1000);
  if (diff < 60) return `${diff}s ago`;
  if (diff < 3600) return `${Math.floor(diff / 60)}m ago`;
  if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`;
  return `${Math.floor(diff / 86400)}d ago`;
}

export function statusLabel(status: string): string {
  const map: Record<string, string> = {
    Waiting: 'Waiting',
    Running: 'Running',
    PendingApproval: 'Needs approval',
    Asking: 'Needs input',
    Done: 'Done',
    Error: 'Error',
  };
  return map[status] ?? status;
}

export function ago(minutes: number): string {
  const d = new Date(Date.now() - minutes * 60 * 1000);
  return d.toISOString();
}
