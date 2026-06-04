import { useNotifications } from '../../hooks/useNotifications';
import { cn } from '../../utils/cn';

export default function NotificationsTab() {
  const { supported, permission, enabled, canEnable, toggle } = useNotifications();

  if (!supported) {
    return (
      <div className="max-w-lg">
        <p className="text-sm text-zinc-500">Notifications are not supported in this browser.</p>
      </div>
    );
  }

  return (
    <div className="space-y-6 max-w-lg">
      {/* Toggle row */}
      <div className="flex items-center justify-between gap-4 p-4 rounded-lg border border-border bg-surface-raised">
        <div className="flex-1 min-w-0">
          <div className="text-sm font-medium text-zinc-800 dark:text-zinc-200">
            Browser notifications
          </div>
          <div className="text-[12px] text-zinc-500 mt-0.5">
            Get notified when a task finishes or needs your input
          </div>
        </div>
        <button
          role="switch"
          aria-checked={enabled}
          disabled={!canEnable}
          onClick={toggle}
          className={cn(
            'relative inline-flex h-6 w-11 flex-none items-center rounded-full transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-500',
            enabled ? 'bg-brand-500' : 'bg-zinc-300 dark:bg-zinc-600',
            !canEnable && 'opacity-40 cursor-not-allowed'
          )}
        >
          <span
            className={cn(
              'inline-block h-4 w-4 rounded-full bg-white shadow transition-transform',
              enabled ? 'translate-x-6' : 'translate-x-1'
            )}
          />
        </button>
      </div>

      {/* Permission status */}
      {permission === 'granted' && (
        <div className="flex items-center gap-2 text-[12px] text-emerald-600 dark:text-emerald-400">
          <span className="inline-block w-2 h-2 rounded-full bg-emerald-500" />
          Notifications allowed
        </div>
      )}
      {permission === 'default' && (
        <div className="flex items-center gap-2 text-[12px] text-amber-600 dark:text-amber-400">
          <span className="inline-block w-2 h-2 rounded-full bg-amber-500" />
          Permission required — enabling will prompt your browser
        </div>
      )}
      {permission === 'denied' && (
        <div className="space-y-1">
          <div className="flex items-center gap-2 text-[12px] text-red-600 dark:text-red-400">
            <span className="inline-block w-2 h-2 rounded-full bg-red-500" />
            Permission denied in browser
          </div>
          <p className="text-[12px] text-zinc-500 pl-4">
            To enable notifications, click the lock icon in your browser's address bar and allow notifications for this site.
          </p>
        </div>
      )}
    </div>
  );
}
