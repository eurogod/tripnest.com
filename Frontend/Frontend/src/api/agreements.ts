import type { Agreement } from '../types';
import { apiDownload, apiGet, apiGetList, apiPost } from './client';
import { mapAgreement, type AgreementResponseDto } from './backend';
import { getBookings } from './bookings';

export async function getAgreements(): Promise<Agreement[]> {
  // Agreements reference bookings; join for property names and dates.
  const [dtos, bookings] = await Promise.all([
    apiGetList<AgreementResponseDto>('/api/agreements/mine'),
    getBookings().catch(() => []),
  ]);
  const byId = new Map(bookings.map((b) => [b.id, b]));
  return dtos.map((dto) => mapAgreement(dto, byId.get(dto.bookingId)));
}

/** One agreement by id — the caller must be a party to it. */
export async function getAgreement(id: string): Promise<Agreement> {
  const dto = await apiGet<AgreementResponseDto>(`/api/agreements/${id}`);
  return mapAgreement(dto);
}

/**
 * Opens the agreement for a CONFIRMED booking. The backend generates the terms text and
 * refuses a second agreement for the same booking (409 Conflict).
 */
export async function createAgreement(bookingId: string): Promise<Agreement> {
  const dto = await apiPost<AgreementResponseDto>('/api/agreements', { bookingId });
  return mapAgreement(dto);
}

/**
 * Signs as the calling party. Signing is two-party: the agreement stays Pending until BOTH
 * tenant and landlord have signed, then flips to Signed. The server snapshots the caller's
 * profile signature image and refuses to bind if the terms changed since the first signature.
 */
export function signAgreement(id: string): Promise<unknown> {
  return apiPost(`/api/agreements/${id}/sign`);
}

/** Ends a signed agreement. Either party may do it, and the backend REQUIRES a reason. */
export function terminateAgreement(id: string, reason: string): Promise<unknown> {
  return apiPost(`/api/agreements/${id}/terminate`, { reason });
}

/** Plain-language AI explanation of the agreement. Advisory — the signed terms stay binding. */
export interface AgreementSummary {
  summary: string;
  keyTerms: string[];
  yourObligations: string[];
  disclaimer: string;
}

export function getAgreementSummary(id: string): Promise<AgreementSummary> {
  return apiGet<AgreementSummary>(`/api/agreements/${id}/summary`);
}

/** The real signed PDF (with both parties' signature images), served by the backend. */
export function downloadAgreementPdf(id: string): Promise<void> {
  return apiDownload(`/api/agreements/${id}/download`, `tripnest-agreement-${id}.pdf`);
}
