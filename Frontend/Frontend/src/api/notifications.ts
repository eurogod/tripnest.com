import type { Notification } from '../types';
import { apiGet, apiPatch } from './client';
import { mapNotification, type NotificationResponseDto, type PagedResultDto } from './backend';

export async function getNotifications(): Promise<Notification[]> {
  const page = await apiGet<PagedResultDto<NotificationResponseDto>>(
    '/api/notifications/mine?page=1&pageSize=50',
  );
  return page.items.map(mapNotification);
}

export async function getUnreadCount(): Promise<number> {
  const data = await apiGet<{ unreadCount: number }>('/api/notifications/unread-count');
  return data.unreadCount;
}

// The sidebar badge asks for the unread count on every mount (i.e. every full
// page load); the endpoint is rate-limited server-side, so share one result
// across mounts — and, via sessionStorage, across reloads — for a minute
// instead of re-fetching. Errors resolve to 0: a missing badge, never a
// broken sidebar.
const UNREAD_TTL_MS = 60_000;
const UNREAD_KEY = 'tripnest.unreadCount';
let unreadInFlight: { userId: string; value: Promise<number> } | null = null;

export function getUnreadCountCached(userId: string): Promise<number> {
  try {
    const raw = sessionStorage.getItem(UNREAD_KEY);
    if (raw) {
      const cached = JSON.parse(raw) as { userId: string; at: number; count: number };
      if (cached.userId === userId && Date.now() - cached.at <= UNREAD_TTL_MS) {
        return Promise.resolve(cached.count);
      }
    }
  } catch { /* unreadable cache — fall through to fetch */ }

  if (!unreadInFlight || unreadInFlight.userId !== userId) {
    unreadInFlight = {
      userId,
      value: getUnreadCount()
        .then((count) => {
          try {
            sessionStorage.setItem(UNREAD_KEY, JSON.stringify({ userId, at: Date.now(), count }));
          } catch { /* storage full/blocked — badge still works this load */ }
          return count;
        })
        .catch(() => 0)
        .finally(() => { unreadInFlight = null; }),
    };
  }
  return unreadInFlight.value;
}

export function markNotificationRead(id: string | number): Promise<unknown> {
  return apiPatch(`/api/notifications/${id}/read`);
}

export function markAllNotificationsRead(): Promise<unknown> {
  return apiPatch('/api/notifications/mark-all-read');
}
