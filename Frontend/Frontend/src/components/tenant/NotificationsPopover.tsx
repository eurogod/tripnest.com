import { useEffect, useRef, useState } from 'react';
import type { Notification, NotificationType } from '../../types';
import { getNotifications, markAllNotificationsRead, markNotificationRead } from '../../api/notifications';
import {
  CalendarIcon, CardIcon, ToolIcon, MessageIcon, ShieldIcon,
} from './icons';

const ICONS: Record<NotificationType, React.ReactNode> = {
  booking: <CalendarIcon size={16} />,
  payment: <CardIcon size={16} />,
  maintenance: <ToolIcon size={16} />,
  message: <MessageIcon size={16} />,
  safety: <ShieldIcon size={16} />,
};

const CHIPS: Record<NotificationType, string> = {
  booking: 'bg-brand-50 text-brand',
  payment: 'bg-brand-50 text-brand',
  maintenance: 'bg-brand-50 text-brand',
  message: 'bg-brand-50 text-brand',
  safety: 'bg-amber-100 text-amber-700',
};

interface NotificationsPopoverProps {
  onClose: () => void;
  onSelect: (notification: Notification) => void;
}

/** Anchored dropdown listing recent notifications; opening one hands off to a full-screen reader. */
export default function NotificationsPopover({ onClose, onSelect }: NotificationsPopoverProps) {
  const [rows, setRows] = useState<Notification[] | null>(null);
  const [error, setError] = useState(false);
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    let active = true;
    getNotifications()
      .then((data) => { if (active) setRows(data); })
      .catch(() => { if (active) setError(true); });
    return () => { active = false; };
  }, []);

  useEffect(() => {
    const onPointerDown = (e: MouseEvent) => {
      if (panelRef.current && !panelRef.current.contains(e.target as Node)) onClose();
    };
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('mousedown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('mousedown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [onClose]);

  const unread = rows?.filter((n) => !n.read).length ?? 0;

  const markAllRead = () => {
    setRows((rs) => rs?.map((n) => ({ ...n, read: true })) ?? rs);
    void markAllNotificationsRead().catch(() => { /* optimistic; next open shows truth */ });
  };

  const openNotification = (n: Notification) => {
    if (!n.read) {
      setRows((rs) => rs?.map((row) => (row.id === n.id ? { ...row, read: true } : row)) ?? rs);
      void markNotificationRead(n.id).catch(() => { /* optimistic; next open shows truth */ });
    }
    onSelect({ ...n, read: true });
  };

  return (
    <div
      ref={panelRef}
      role="dialog"
      aria-label="Notifications"
      className="absolute right-0 top-full z-[60] mt-3 w-80 max-w-[calc(100vw-2rem)] overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-xl"
    >
      <div className="flex items-center justify-between border-b border-gray-100 px-4 py-3">
        <p className="font-semibold text-ink">Notifications</p>
        {unread > 0 && (
          <button onClick={markAllRead} className="text-xs font-semibold text-brand hover:underline">
            Mark all read
          </button>
        )}
      </div>

      <div className="max-h-96 overflow-y-auto">
        {rows === null && !error && (
          <p className="px-4 py-6 text-center text-sm text-muted">Loading…</p>
        )}
        {error && (
          <p className="px-4 py-6 text-center text-sm text-muted">Couldn&apos;t load notifications.</p>
        )}
        {rows && rows.length === 0 && (
          <p className="px-4 py-6 text-center text-sm text-muted">You&apos;re all caught up.</p>
        )}
        {rows?.map((n) => (
          <button
            key={n.id}
            onClick={() => openNotification(n)}
            className={`flex w-full gap-3 border-b border-gray-50 px-4 py-3 text-left transition-colors last:border-b-0 hover:bg-gray-50 ${
              n.read ? '' : n.type === 'safety' ? 'bg-amber-50/60' : 'bg-brand-50/40'
            }`}
          >
            <span className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${CHIPS[n.type]}`}>
              {ICONS[n.type]}
            </span>
            <div className="min-w-0 flex-1">
              <div className="flex items-center justify-between gap-2">
                <p className="truncate text-sm font-semibold text-ink">{n.title}</p>
                <span className="shrink-0 text-xs text-muted">{n.time}</span>
              </div>
              <p className="truncate text-xs text-muted">{n.body}</p>
            </div>
            {!n.read && (
              <span className={`mt-1.5 h-2 w-2 shrink-0 rounded-full ${n.type === 'safety' ? 'bg-amber-500' : 'bg-brand'}`} />
            )}
          </button>
        ))}
      </div>
    </div>
  );
}
