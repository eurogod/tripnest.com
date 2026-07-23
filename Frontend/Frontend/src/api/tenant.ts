import type { TenantDashboard } from '../types';
import { apiGet, apiGetList } from './client';
import { formatIsoDate, mapBookingStatus, type TenantDashboardDto, type WishlistItemDto } from './backend';
import { getMaintenanceTickets } from './maintenance';
import { getConversations } from './messages';
import { getPropertyById } from './properties';

/**
 * The tenant dashboard page spans several backend surfaces; compose
 * /api/personaldashboard/tenant with wishlist, maintenance and chat.
 * (The safety card manages its own trusted-contact state via api/safety.)
 */
export async function getTenantDashboard(): Promise<TenantDashboard> {
  const [dash, wishlist, tickets, conversations] = await Promise.all([
    apiGet<TenantDashboardDto>('/api/personaldashboard/tenant'),
    apiGetList<WishlistItemDto>('/api/wishlist/mine').catch(() => []),
    getMaintenanceTickets().catch(() => []),
    getConversations().catch(() => []),
  ]);

  const next = dash.recentBookings.find((b) => mapBookingStatus(b.status, b.checkInDate) === 'upcoming');
  const nextProperty = next ? await getPropertyById(next.propertyId) : undefined;

  const latestTicket = tickets[0];
  return {
    stats: {
      activeBookings: dash.activeBookings,
      rentPaid: dash.totalSpent,
      savedProperties: wishlist.length,
      openMaintenance: tickets.filter((t) => t.status !== 'resolved').length,
    },
    upcoming: {
      title: nextProperty?.title ?? (next ? 'Upcoming stay' : 'No upcoming stay'),
      bookingId: next?.id ?? '',
      propertyId: next?.propertyId ?? '',
      location: nextProperty?.location ?? '',
      dates: next ? `${formatIsoDate(next.checkInDate)} – ${formatIsoDate(next.checkOutDate)}` : '—',
      price: next?.totalAmount ?? 0,
      period: 'month',
      status: next ? 'Confirmed' : '—',
    },
    maintenance: {
      pending: tickets.filter((t) => t.status === 'pending').length,
      inProgress: tickets.filter((t) => t.status === 'in-progress').length,
      resolved: tickets.filter((t) => t.status === 'resolved').length,
      latest: latestTicket
        ? { title: latestTicket.title, reportedOn: latestTicket.reportedOn, status: latestTicket.status }
        : { title: 'No reports yet', reportedOn: '—', status: '—' },
    },
    messages: conversations.slice(0, 3).map((c, i) => ({
      id: i,
      name: c.name,
      role: c.role,
      preview: c.lastMessage || 'Open conversation',
      time: c.time,
    })),
  };
}
