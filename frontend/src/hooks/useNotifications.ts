import { useCallback, useEffect, useState } from 'react';

const STORAGE_KEY = 'autocoder_notifications_enabled';

const isSupported = typeof Notification !== 'undefined';

function readEnabled(): boolean {
  if (!isSupported) return false;
  try {
    return localStorage.getItem(STORAGE_KEY) === 'true';
  } catch {
    return false;
  }
}

function writeEnabled(value: boolean) {
  try {
    localStorage.setItem(STORAGE_KEY, String(value));
  } catch {
    // ignore
  }
}

export interface NotificationsState {
  supported: boolean;
  permission: NotificationPermission;
  enabled: boolean;
  canEnable: boolean;
  toggle(): Promise<void>;
  notify(title: string, options?: NotificationOptions): void;
}

export function useNotifications(): NotificationsState {
  const [permission, setPermission] = useState<NotificationPermission>(
    isSupported ? Notification.permission : 'denied'
  );
  const [enabled, setEnabled] = useState<boolean>(() => {
    const stored = readEnabled();
    if (stored && isSupported && Notification.permission === 'denied') {
      writeEnabled(false);
      return false;
    }
    return stored;
  });

  // Sync permission when tab becomes visible — catches external revocation from browser settings
  useEffect(() => {
    if (!isSupported) return;
    const sync = () => {
      const current = Notification.permission;
      setPermission(current);
      if (current === 'denied' && enabled) {
        setEnabled(false);
        writeEnabled(false);
      }
    };
    document.addEventListener('visibilitychange', sync);
    return () => document.removeEventListener('visibilitychange', sync);
  }, [enabled]);

  const toggle = useCallback(async () => {
    if (!isSupported) return;

    if (enabled) {
      setEnabled(false);
      writeEnabled(false);
      return;
    }

    const current = Notification.permission;
    if (current === 'denied') return;

    if (current === 'granted') {
      setEnabled(true);
      writeEnabled(true);
      return;
    }

    // 'default' — request permission
    const result = await Notification.requestPermission();
    setPermission(result);
    if (result === 'granted') {
      setEnabled(true);
      writeEnabled(true);
    }
  }, [enabled]);

  const notify = useCallback((title: string, options?: NotificationOptions) => {
    if (!isSupported || !enabled || permission !== 'granted') return;
    const n = new Notification(title, { icon: '/vite.svg', ...options });
    n.onclick = () => window.focus();
  }, [enabled, permission]);

  return {
    supported: isSupported,
    permission,
    enabled,
    canEnable: isSupported && permission !== 'denied',
    toggle,
    notify,
  };
}
