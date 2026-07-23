import { apiGet, apiGetList, apiPost, apiUpload } from './client';

// Damage claims: the landlord files against a completed stay (with photo evidence), the tenant
// gets to respond, and an admin decides — approving pays out from the platform's claim flow.

export const CLAIM_STATUS = ['Submitted', 'Approved', 'Rejected'] as const;

export interface DamageClaimDto {
  claimId: string;
  bookingId: string;
  landlordId: string;
  tenantId: string;
  amount: number;
  approvedAmount?: number | null;
  description: string;
  photoPaths: string[];
  status: number; // index into CLAIM_STATUS
  tenantResponse?: string | null;
  resolutionNote?: string | null;
  createdAt: string;
  resolvedAt?: string | null;
}

/** Landlord: file a claim on a completed booking, with optional evidence photos. */
export function fileClaim(bookingId: string, amount: number, description: string, photos: File[]): Promise<DamageClaimDto> {
  const form = new FormData();
  form.append('bookingId', bookingId);
  form.append('amount', String(amount));
  form.append('description', description);
  photos.forEach((p) => form.append('photos', p));
  return apiUpload<DamageClaimDto>('/api/claims', form);
}

/** Landlord: my filed claims, newest first. */
export function getMyClaims(): Promise<DamageClaimDto[]> {
  return apiGetList<DamageClaimDto>('/api/claims/mine');
}

/** Claims on one booking — how a tenant discovers a claim against their stay. */
export function getBookingClaims(bookingId: string): Promise<DamageClaimDto[]> {
  return apiGetList<DamageClaimDto>(`/api/claims/booking/${bookingId}`);
}

/** Tenant: add your side of the story before the admin decides. */
export function respondToClaim(claimId: string, response: string): Promise<DamageClaimDto> {
  return apiPost<DamageClaimDto>(`/api/claims/${claimId}/respond`, { response });
}

// ---- Admin review ---------------------------------------------------------------------------

export function getClaimsForReview(): Promise<DamageClaimDto[]> {
  return apiGetList<DamageClaimDto>('/api/claims/review');
}

/** Approve, optionally for less than asked; the note is shown to both parties. */
export function approveClaim(claimId: string, approvedAmount?: number, note?: string): Promise<DamageClaimDto> {
  return apiPost<DamageClaimDto>(`/api/claims/${claimId}/approve`, { approvedAmount, note });
}

export function rejectClaim(claimId: string, reason: string): Promise<DamageClaimDto> {
  return apiPost<DamageClaimDto>(`/api/claims/${claimId}/reject`, { reason });
}

/** AI reading brief for the reviewing admin — advisory; the human decides. */
export interface AdminBrief {
  brief: string;
  keyPoints: string[];
  inconsistencies: string[];
  disclaimer: string;
}

export function getClaimBrief(claimId: string): Promise<AdminBrief> {
  return apiGet<AdminBrief>(`/api/claims/${claimId}/brief`);
}
