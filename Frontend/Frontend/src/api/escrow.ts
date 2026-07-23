import { apiGet, apiPost } from './client';

// Escrow-backed payments (Paystack). The server derives the amount from the
// booking; the browser only initiates and then reflects status — the Paystack
// webhook is the source of truth for funds moving into escrow.

export const ESCROW_STATUS = {
  pending: 0,
  heldInEscrow: 1,
  released: 2,
  refunded: 3,
  disputed: 4,
} as const;

export interface EscrowResponseDto {
  escrowId: string;
  bookingId: string;
  amount: number;
  status: number; // EscrowStatus, see ESCROW_STATUS
  createdAt: string;
  heldAt?: string | null;
  releasedAt?: string | null;
  releaseReason?: string | null;
  paymentReference?: string | null;
  checkoutUrl?: string | null; // set on initiate only
}

export function initiateEscrow(bookingId: string): Promise<EscrowResponseDto> {
  return apiPost<EscrowResponseDto>('/api/escrow/initiate', { bookingId });
}

/**
 * Actively verify a booking's payment and hold the funds if it succeeded. Used by the
 * dev/simulated checkout (no Paystack redirect) to complete the flow — the backend's simulated
 * gateway reports success and holds the escrow, confirming the booking.
 */
export function verifyEscrowPayment(bookingId: string): Promise<EscrowResponseDto> {
  return apiPost<EscrowResponseDto>(`/api/escrow/booking/${bookingId}/verify`);
}

export function getEscrow(escrowId: string): Promise<EscrowResponseDto> {
  return apiGet<EscrowResponseDto>(`/api/escrow/${escrowId}`);
}

/** True for the placeholder URL the backend returns when Paystack isn't configured (dev). */
export function isSimulatedCheckout(url: string | null | undefined): boolean {
  return !!url && url.includes('checkout.paystack.test');
}

// The checkout in flight, so the Paystack return page can pick polling back up
// after the full-page redirect away to the hosted checkout.

const PENDING_KEY = 'tripnest.pendingCheckout';

export interface PendingCheckout {
  escrowId: string;
  bookingId: string;
  propertyTitle: string;
}

export function savePendingCheckout(pending: PendingCheckout): void {
  try { sessionStorage.setItem(PENDING_KEY, JSON.stringify(pending)); } catch { /* ignore */ }
}

export function readPendingCheckout(): PendingCheckout | null {
  try {
    const raw = sessionStorage.getItem(PENDING_KEY);
    return raw ? (JSON.parse(raw) as PendingCheckout) : null;
  } catch {
    return null;
  }
}

export function clearPendingCheckout(): void {
  try { sessionStorage.removeItem(PENDING_KEY); } catch { /* ignore */ }
}
