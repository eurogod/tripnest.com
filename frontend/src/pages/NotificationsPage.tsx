import { useQuery, useQueryClient } from '@tanstack/react-query';
import { notificationsApi } from '@/lib/services';
import { PageHeader, Async, Row } from '@/components/dashboard';
import { Button } from '@/components/ui';
import { Bell, Check, X } from '@/components/icons';
import { relativeTime } from '@/lib/format';
import { useAuth } from '@/auth/AuthContext';
import { useToast } from '@/components/Toast';

export default function NotificationsPage() {
  const { user } = useAuth();
  const qc = useQueryClient();
  const toast = useToast();

  const query = useQuery({
    queryKey: ['notifications'],
    queryFn: () => notificationsApi.mine(1, 50),
    enabled: !!user,
  });

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ['notifications'] });
    qc.invalidateQueries({ queryKey: ['unread-count'] });
  };

  async function markAll() {
    await notificationsApi.markAllRead();
    toast.success('All caught up');
    refresh();
  }

  return (
    <div className="container-tn max-w-3xl py-8">
      <PageHeader
        title="Notifications"
        subtitle="Booking, payment, safety and verification updates."
        action={
          <Button variant="outline" size="sm" onClick={markAll}>
            <Check className="h-4 w-4" /> Mark all read
          </Button>
        }
      />
      <Async
        query={query}
        isEmpty={(d) => d.items.length === 0}
        emptyIcon={<Bell className="h-6 w-6" />}
        emptyTitle="No notifications"
        emptySubtitle="We’ll let you know when something needs your attention."
      >
        {(data) => (
          <div className="space-y-2.5">
            {data.items.map((n) => (
              <Row
                key={n.notificationId}
                icon={<Bell className="h-5 w-5" />}
                title={
                  <span className="flex items-center gap-2">
                    {n.title}
                    {!n.isRead && <span className="h-2 w-2 rounded-full bg-brand-600" aria-label="unread" />}
                  </span>
                }
                subtitle={n.message}
                meta={<span className="text-xs text-muted">{relativeTime(n.createdAt)}</span>}
                action={
                  <div className="flex gap-1">
                    {!n.isRead && (
                      <button
                        onClick={async () => {
                          await notificationsApi.markRead(n.notificationId);
                          refresh();
                        }}
                        className="rounded-full p-1.5 text-muted hover:bg-black/5"
                        aria-label="Mark read"
                      >
                        <Check className="h-4 w-4" />
                      </button>
                    )}
                    <button
                      onClick={async () => {
                        await notificationsApi.remove(n.notificationId);
                        refresh();
                      }}
                      className="rounded-full p-1.5 text-muted hover:bg-black/5"
                      aria-label="Delete"
                    >
                      <X className="h-4 w-4" />
                    </button>
                  </div>
                }
              />
            ))}
          </div>
        )}
      </Async>
    </div>
  );
}
