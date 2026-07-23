import { useState } from 'react';
import type { Notification, NotificationType } from '../../types';
import { getNotifications, markAllNotificationsRead, markNotificationRead } from '../../api/notifications';
import { useAsync } from '../../hooks/useAsync';
import { useT } from '../../lib/i18n';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import Badge from '../../components/ui/Badge';
import {
  CalendarIcon, CardIcon, ToolIcon, MessageIcon, ShieldIcon,
} from '../../components/tenant/icons';

const ICONS: Record<NotificationType, React.ReactNode> = {
  booking: <CalendarIcon size={18} />,
  payment: <CardIcon size={18} />,
  maintenance: <ToolIcon size={18} />,
  message: <MessageIcon size={18} />,
  safety: <ShieldIcon size={18} />,
};

// Safety alerts (e.g. off-platform payment warnings) get an amber treatment.
const CHIPS: Record<NotificationType, string> = {
  booking: 'bg-brand-50 text-brand',
  payment: 'bg-brand-50 text-brand',
  maintenance: 'bg-brand-50 text-brand',
  message: 'bg-brand-50 text-brand',
  safety: 'bg-amber-100 text-amber-700',
};

function NotificationsView({ initial }: { initial: Notification[] }) {
  const [rows, setRows] = useState(initial);
  const unread = rows.filter((n) => !n.read).length;

  const markAllRead = () => {
    setRows((rs) => rs.map((n) => ({ ...n, read: true })));
    void markAllNotificationsRead().catch(() => { /* optimistic; refetch shows truth */ });
  };
  const markRead = (id: string | number) => {
    setRows((rs) => rs.map((n) => (n.id === id ? { ...n, read: true } : n)));
    void markNotificationRead(id).catch(() => { /* optimistic; refetch shows truth */ });
  };

  return (
    <>
      <div className="mb-4 flex items-center justify-between">
        <p className="text-sm text-muted">
          {unread > 0 ? `${unread} unread` : 'All caught up'}
        </p>
        {unread > 0 && (
          <button onClick={markAllRead} className="text-sm font-semibold text-brand hover:underline">
            Mark all read
          </button>
        )}
      </div>
      <Card className="divide-y divide-gray-100 overflow-hidden">
        {rows.map((n) => (
          <button
            key={n.id}
            onClick={() => markRead(n.id)}
            className={`flex w-full gap-3 px-5 py-4 text-left transition-colors hover:bg-gray-50 ${
              n.read ? '' : n.type === 'safety' ? 'bg-amber-50/60' : 'bg-brand-50/40'
            }`}
          >
            <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-lg ${CHIPS[n.type]}`}>
              {ICONS[n.type]}
            </span>
            <div className="min-w-0 flex-1">
              <div className="flex items-center justify-between gap-2">
                <p className="flex items-center gap-2 font-semibold text-ink">
                  {n.title}
                  {n.type === 'safety' && <Badge tone="amber">Safety</Badge>}
                </p>
                <span className="shrink-0 text-xs text-muted">{n.time}</span>
              </div>
              <p className="text-sm text-muted">{n.body}</p>
            </div>
            {!n.read && (
              <span className={`mt-1.5 h-2 w-2 shrink-0 rounded-full ${n.type === 'safety' ? 'bg-amber-500' : 'bg-brand'}`} />
            )}
          </button>
        ))}
      </Card>
    </>
  );
}

export default function NotificationsPage() {
  const state = useAsync(getNotifications, []);
  const t = useT();

  return (
    <div className="max-w-3xl">
      <h1 className="mb-6 text-3xl font-bold text-ink">Notifications</h1>

      <AsyncBoundary
        state={state}
        loadingMessage="Loading notifications…"
        errorMessage="Failed to load notifications."
        emptyMessage={t("You're all caught up.")}
        isEmpty={(rows) => rows.length === 0}
      >
        {(rows) => <NotificationsView initial={rows} />}
      </AsyncBoundary>
    </div>
  );
}
