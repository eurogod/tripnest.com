import type { Property } from '../types';
import { nightsBetween } from '../lib/format';

// Client-side price quoting for the booking widget and checkout summary.
// (The in-memory booking/reservation mirrors that used to live here are gone —
// bookings and host reservations now come from TripNest.Core.)

const SERVICE_FEE_RATE = 0.05;

export interface PriceBreakdown {
  nights: number;
  perNight: number;
  subtotal: number;
  serviceFee: number;
  total: number;
}

/** Prorate a per-period listing price across the selected nights. */
export function quotePrice(
  property: Pick<Property, 'price' | 'period'>,
  checkInISO: string,
  checkOutISO: string,
): PriceBreakdown {
  const nights = nightsBetween(checkInISO, checkOutISO);
  const nightsPerPeriod = property.period === 'month' ? 30 : 7;
  const perNight = Math.round(property.price / nightsPerPeriod);
  const subtotal = perNight * nights;
  const serviceFee = Math.round(subtotal * SERVICE_FEE_RATE);
  return { nights, perNight, subtotal, serviceFee, total: subtotal + serviceFee };
}
