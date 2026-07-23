import type { OverviewSummary } from '../types';
import { apiGet } from './client';
import {
  mapReservationRow,
  type LandlordBookingResponseDto,
  type LandlordEarningsDto,
  type PagedResultDto,
} from './backend';

interface LandlordStatsDto {
  totalProperties: number;
  activeProperties: number;
  totalBookings: number;
   activeBookings: number;
  completedBookings: number;
}

/** Nights of a booking that fall inside the current calendar month. */
function nightsThisMonth(checkInIso: string, checkOutIso: string): number {
  const now = new Date();
  const monthStart = new Date(now.getFullYear(), now.getMonth(), 1).getTime();
  const monthEnd = new Date(now.getFullYear(), now.getMonth() + 1, 1).getTime();
  const start = Math.max(new Date(checkInIso).getTime(), monthStart);
  const end = Math.min(new Date(checkOutIso).getTime(), monthEnd);
  return Math.max(0, Math.round((end - start) / 86_400_000));
}

// Overview aggregates assembled from the landlord dashboard endpoints:
// earnings totals, property stats (for occupancy), and the bookings list
// (upcoming count, average rate, recent rows).
export async function getOverview(): Promise<OverviewSummary> {
  const [earnings, stats, bookingsPage] = await Promise.all([
    apiGet<LandlordEarningsDto>('/api/landlord/earnings'),
    apiGet<LandlordStatsDto>('/api/landlord/stats'),
    apiGet<PagedResultDto<LandlordBookingResponseDto>>('/api/landlord/bookings?page=1&pageSize=50'),
  ]);
  const bookings = bookingsPage.items;
  const active = bookings.filter((b) => b.stage !== 'Canceled');

  const daysInMonth = new Date(new Date().getFullYear(), new Date().getMonth() + 1, 0).getDate();
  const bookedNights = active.reduce((sum, b) => sum + nightsThisMonth(b.checkIn, b.checkOut), 0);
  const capacity = Math.max(1, stats.activeProperties) * daysInMonth;

  const rated = active.filter((b) => b.nights > 0);
  const avgNightlyRate = rated.length > 0
    ? Math.round(rated.reduce((sum, b) => sum + b.amount / b.nights, 0) / rated.length)
    : 0;

  return {
    monthlyEarnings: earnings.thisMonthEarnings,
    occupancyRate: Math.min(100, Math.round((bookedNights / capacity) * 100)),
    upcomingCount: bookings.filter((b) => b.stage === 'Upcoming').length,
    avgNightlyRate,
    recent: bookings.slice(0, 4).map(mapReservationRow),
  };
}
