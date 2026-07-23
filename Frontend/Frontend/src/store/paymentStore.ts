import type { PaymentChannel, Transaction, TransactionStatus } from '../types';

// ---------------------------------------------------------------------------
// In-memory payment store (Paystack-shaped). Mirrors the bookingStore pattern:
// pure functions over module state, wrapped with mockResponse in api/payments.
//
// Real flow (slice 3+): initiatePayment hits the BACKEND, which calls Paystack
// `transaction/initialize` with the secret key (never exposed here). For cards
// the backend returns an authorization_url to redirect to; for Mobile Money the
// user approves a prompt on their phone. verifyPayment then hits the backend's
// `transaction/verify/:reference`, and a signed webhook is the real source of
// truth. Swap the bodies below for those calls — call sites won't change.
// ---------------------------------------------------------------------------

const transactions = new Map<string, Transaction>();
let txnSeq = 5000;

export type MomoNetwork = 'mtn' | 'telecel' | 'airteltigo';

export interface InitiatePaymentInput {
  amount: number;
  channel: PaymentChannel;
  /** Payer email — required by Paystack to open a transaction. */
  email: string;
  /** Mobile Money phone number — required when channel is 'momo'. */
  momoNumber?: string;
  /** Mobile Money network — required when channel is 'momo'. */
  momoNetwork?: MomoNetwork;
  /** Optional booking/order reference this charge settles. */
  bookingRef?: string;
}

export interface InitiateResult {
  reference: string;
  status: TransactionStatus;
  /** Paystack card redirect target (unused by the mock). */
  authorizationUrl?: string;
}

/** Open a pending transaction. Real impl: POST /payments/initiate. */
export function initiatePayment(input: InitiatePaymentInput): InitiateResult {
  const reference = `TN-PAY-${++txnSeq}`;
  const txn: Transaction = {
    reference,
    bookingId: input.bookingRef,
    amount: input.amount,
    currency: 'GHS',
    channel: input.channel,
    provider: 'paystack',
    status: 'pending',
    createdAt: new Date().toISOString(),
  };
  transactions.set(reference, txn);
  return { reference, status: 'pending' };
}

/**
 * Confirm a transaction's outcome. Idempotent — verifying twice returns the
 * settled record. The mock approves any pending charge; the real verify reads
 * the provider's authoritative status.
 */
export function verifyPayment(reference: string): Transaction {
  const txn = transactions.get(reference);
  if (!txn) throw new Error(`Unknown payment reference: ${reference}`);
  if (txn.status === 'pending') txn.status = 'success';
  return { ...txn };
}

/** Link a settled charge to the booking it created (for receipts/reconciliation). */
export function attachBooking(reference: string, bookingId: string): void {
  const txn = transactions.get(reference);
  if (txn) txn.bookingId = bookingId;
}

export function getTransaction(reference: string): Transaction | undefined {
  const txn = transactions.get(reference);
  return txn && { ...txn };
}
