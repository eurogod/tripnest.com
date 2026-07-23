import type { Reservation } from '../types';
import { apiGet } from './client';
import {
  mapReservationDetails,
  mapReservationRow,
  type LandlordBookingResponseDto,
  type PagedResultDto,
  type ReservationDetailsResponseDto,
} from './backend';

// Host reservations, backed by TripNest.Core's landlord workspace. The list
// gives the table rows; the per-booking details call adds the earnings
// breakdown and guest reviews.

export async function getReservations(): Promise<Reservation[]> {
  const page = await apiGet<PagedResultDto<LandlordBookingResponseDto>>('/api/landlord/bookings?page=1&pageSize=50');
  return page.items.map(mapReservationRow);
}

export async function getReservationById(id: string): Promise<Reservation | undefined> {
  const dto = await apiGet<ReservationDetailsResponseDto>(`/api/landlord/reservations/${id}`);
  return mapReservationDetails(dto);
}
