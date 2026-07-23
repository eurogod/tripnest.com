import type { Inquiry, InquiryStatus, LandlordBooking, LandlordReview, LandlordTenant } from '../types';
import { apiGet, apiPatch, apiPost } from './client';
import {
  formatIsoDate,
  mapInquiry,
  mapLandlordBooking,
  mapLandlordTenant,
  type InquiryResponseDto,
  type LandlordBookingResponseDto,
  type LandlordTenantResponseDto,
  type PagedResultDto,
  type ReviewResponseDto,
} from './backend';
import { getListings } from './listings';

// Landlord workspace service layer, backed by TripNest.Core's /api/landlord
// endpoints (bookings, tenants, inquiries) plus /api/reviews per listing.

export async function getInquiries(): Promise<Inquiry[]> {
  const page = await apiGet<PagedResultDto<InquiryResponseDto>>('/api/landlord/inquiries?page=1&pageSize=50');
  return page.items.map(mapInquiry);
}

/** Backend enum names are capitalized: new → New. */
export function setInquiryStatus(id: string, status: InquiryStatus): Promise<unknown> {
  const name = status.charAt(0).toUpperCase() + status.slice(1);
  return apiPatch(`/api/landlord/inquiries/${id}/status`, { status: name });
}

export async function getLandlordBookings(): Promise<LandlordBooking[]> {
  const page = await apiGet<PagedResultDto<LandlordBookingResponseDto>>('/api/landlord/bookings?page=1&pageSize=50');
  return page.items.map(mapLandlordBooking);
}

/** Landlord declines a booking — the guest is always refunded in full. */
export function declineLandlordBooking(bookingId: string): Promise<unknown> {
  return apiPost(`/api/bookings/${bookingId}/cancel`);
}

export async function getLandlordTenants(): Promise<LandlordTenant[]> {
  const page = await apiGet<PagedResultDto<LandlordTenantResponseDto>>('/api/landlord/tenants?page=1&pageSize=50');
  return page.items.map(mapLandlordTenant);
}

// Reviews have no landlord-scoped endpoint; gather the public per-property
// lists across the landlord's own listings. Reviewer names aren't in the DTO.
export async function getLandlordReviews(): Promise<LandlordReview[]> {
  const listings = await getListings();
  const perListing = await Promise.all(
    listings.map(async (listing) => {
      const page = await apiGet<PagedResultDto<ReviewResponseDto>>(
        `/api/reviews/property/${listing.id}?page=1&pageSize=50`,
      ).catch(() => null);
      return (page?.items ?? []).map((dto) => ({
        id: dto.reviewId,
        guest: `Guest ${dto.reviewerId.slice(0, 8)}`,
        listing: listing.title,
        rating: dto.rating,
        date: formatIsoDate(dto.createdAt),
        text: dto.comment,
        createdAt: dto.createdAt,
      }));
    }),
  );
  return perListing
    .flat()
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
    .map(({ createdAt: _createdAt, ...review }) => review);
}
