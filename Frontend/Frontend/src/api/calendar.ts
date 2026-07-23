import type { CalendarBooking, CalendarMonth, Listing } from '../types';
import { apiDelete, apiGet, apiGetList, apiPost } from './client';
import type { BlockedDateDto } from './backend';

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

const iso = (year: number, monthIndex: number, day: number) =>
  `${year}-${String(monthIndex + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`;

/** Open (bookable) range; `end` is exclusive. */
export interface DateRangeDto {
  start: string;
  end: string;
}

export function getAvailableRanges(
  propertyId: string,
  fromISO: string,
  toISO: string,
): Promise<DateRangeDto[]> {
  return apiGetList<DateRangeDto>(
    `/api/properties/${propertyId}/available-ranges?from=${fromISO}&to=${toISO}`,
  );
}

/**
 * True when one open range covers the whole stay [checkIn, checkOut).
 * Mirrors the server's rule: confirmed bookings + landlord blocks make days unavailable.
 */
export async function isSpanAvailable(
  propertyId: string,
  checkInISO: string,
  checkOutISO: string,
): Promise<boolean> {
  const ranges = await getAvailableRanges(propertyId, checkInISO, checkOutISO);
  // Compare YYYY-MM-DD prefixes: the API's zone-less datetimes would parse as
  // local time while date-only inputs parse as UTC, skewing Date comparisons.
  return ranges.some(
    (r) => r.start.slice(0, 10) <= checkInISO && checkOutISO <= r.end.slice(0, 10),
  );
}

/**
 * Build one calendar month for a listing from the real availability endpoint.
 * The backend tracks blocked-date ranges (with a free-text reason) and a flat
 * daily rate — per-day pricing, discounts and booking bars have no backend yet.
 */
export async function getCalendarMonth(
  listing: Listing,
  year: number,
  monthIndex: number,
): Promise<CalendarMonth> {
  const daysInMonth = new Date(year, monthIndex + 1, 0).getDate();
  const monthStart = iso(year, monthIndex, 1);
  const monthEnd = iso(year, monthIndex, daysInMonth);
  const [blocked, openRanges] = await Promise.all([
    apiGetList<BlockedDateDto>(
      `/api/properties/${listing.id}/availability?startDate=${monthStart}&endDate=${monthEnd}`,
    ),
    getAvailableRanges(listing.id, monthStart, monthEnd),
  ]);

  const ownerDays = new Set<number>();
  const maintenanceDays = new Set<number>();
  for (const range of blocked) {
    const target = /maint|repair|fix/i.test(range.reason ?? '') ? maintenanceDays : ownerDays;
    const start = new Date(range.startDate);
    const end = new Date(range.endDate);
    const startDay = new Date(start.getFullYear(), start.getMonth(), start.getDate());
    const endDay = new Date(end.getFullYear(), end.getMonth(), end.getDate());
    for (let day = 1; day <= daysInMonth; day++) {
      const date = new Date(year, monthIndex, day);
      if (date >= startDay && date <= endDay) target.add(day);
    }
  }

  // Booked days = days the server reports unavailable that aren't landlord blocks.
  // (available-ranges excludes confirmed bookings + blocked dates; subtracting the
  // blocks we already know leaves the guest bookings.)
  const openDays = new Set<number>();
  for (const range of openRanges) {
    const start = new Date(range.start);
    const end = new Date(range.end); // exclusive
    for (let day = 1; day <= daysInMonth; day++) {
      const date = new Date(year, monthIndex, day);
      if (date >= new Date(start.getFullYear(), start.getMonth(), start.getDate()) &&
          date < new Date(end.getFullYear(), end.getMonth(), end.getDate())) {
        openDays.add(day);
      }
    }
  }

  const bookings: CalendarBooking[] = [];
  let run: { start: number; end: number } | null = null;
  for (let day = 1; day <= daysInMonth; day++) {
    const isBooked = !openDays.has(day) && !ownerDays.has(day) && !maintenanceDays.has(day);
    if (isBooked) {
      if (run) run.end = day;
      else run = { start: day, end: day };
    } else if (run) {
      bookings.push({ startDate: run.start, endDate: run.end, label: `Booked · ${run.end - run.start + 1}d` });
      run = null;
    }
  }
  if (run) bookings.push({ startDate: run.start, endDate: run.end, label: `Booked · ${run.end - run.start + 1}d` });

  const prices: Record<number, number> = {};
  const weekendDays: number[] = [];
  for (let day = 1; day <= daysInMonth; day++) {
    prices[day] = listing.nightlyRate;
    const dow = new Date(year, monthIndex, day).getDay();
    if (dow === 0 || dow === 6) weekendDays.push(day);
  }

  return {
    label: `${MONTHS[monthIndex]} ${year}`,
    minNights: 1,
    weekendDays,
    discountDays: [],
    ownerDays: [...ownerDays],
    maintenanceDays: [...maintenanceDays],
    prices,
    bookings,
  };
}

// ---- iCal sync (export our calendar; import other platforms') -------------------------------

/** The public tokenized .ics URL for this property — paste into Airbnb/Booking/Google Calendar. */
export async function getIcalFeedUrl(propertyId: string): Promise<string> {
  const data = await apiGet<{ feedUrl: string }>(`/api/calendar/${propertyId}/feed-url`);
  return data.feedUrl;
}

export interface ExternalCalendarDto {
  id: string;
  propertyId: string;
  name: string;
  feedUrl: string;
  lastSyncedAt?: string | null;
  lastSyncError?: string | null;
  importedRanges: number;
}

export function getExternalCalendars(propertyId: string): Promise<ExternalCalendarDto[]> {
  return apiGetList<ExternalCalendarDto>(`/api/calendar/${propertyId}/external`);
}

export function addExternalCalendar(propertyId: string, name: string, feedUrl: string): Promise<ExternalCalendarDto> {
  return apiPost<ExternalCalendarDto>(`/api/calendar/${propertyId}/external`, { name, feedUrl });
}

/** Pull the feed now (a background worker also refreshes on a schedule). */
export function syncExternalCalendar(id: string): Promise<ExternalCalendarDto> {
  return apiPost<ExternalCalendarDto>(`/api/calendar/external/${id}/sync`);
}

export function removeExternalCalendar(id: string): Promise<unknown> {
  return apiDelete(`/api/calendar/external/${id}`);
}
