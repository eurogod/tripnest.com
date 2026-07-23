import type { Payment, PaymentMethod, Transaction } from '../types';
import {
  initiatePayment as storeInitiate,
  verifyPayment as storeVerify,
  attachBooking as storeAttachBooking,
  type InitiatePaymentInput,
  type InitiateResult,
  type MomoNetwork,
} from '../store/paymentStore';
import { apiDelete, apiDownload, apiGet, apiGetList, apiPatch, apiPost, mockResponse } from './client';
import {
  formatIsoDate,
  mapPaymentMethod,
  type PagedResultDto,
  type PaymentMethodResponseDto,
  type ReceiptResponseDto,
} from './backend';
import { getBookings } from './bookings';

/** Settled payments = the user's receipts. Joined against bookings for property names. */
export async function getPayments(): Promise<Payment[]> {
  const [paged, bookings] = await Promise.all([
    apiGet<PagedResultDto<ReceiptResponseDto>>('/api/receipts/mine?page=1&pageSize=50'),
    getBookings().catch(() => []),
  ]);
  const propertyByBooking = new Map(bookings.map((b) => [b.id, b.property]));
  return paged.items.map((r) => ({
    id: r.receiptId,
    description: r.description || 'Booking payment',
    property: propertyByBooking.get(r.bookingId) ?? `Booking ${r.bookingId.slice(0, 8).toUpperCase()}`,
    date: formatIsoDate(r.createdAt),
    amount: r.amount,
    method: r.paymentMethod || 'Paystack',
    status: 'paid' as const,
  }));
}

export function downloadReceipt(receiptId: string): Promise<void> {
  return apiDownload(`/api/receipts/${receiptId}/download`, `tripnest-receipt-${receiptId.slice(0, 8)}.pdf`);
}

// Saved payment methods, backed by /api/payments/methods. These are display
// preferences — Paystack's hosted page still collects the instrument per charge.
export async function getPaymentMethods(): Promise<PaymentMethod[]> {
  const dtos = await apiGetList<PaymentMethodResponseDto>('/api/payments/methods');
  return dtos.map(mapPaymentMethod);
}

export async function addPaymentMethod(
  provider: string,
  maskedNumber: string,
  channel: 'momo' | 'card',
  makePrimary = false,
): Promise<PaymentMethod> {
  const dto = await apiPost<PaymentMethodResponseDto>('/api/payments/methods', {
    provider,
    maskedNumber,
    channel,
    makePrimary,
  });
  return mapPaymentMethod(dto);
}

export function setPrimaryPaymentMethod(id: string): Promise<unknown> {
  return apiPatch(`/api/payments/methods/${id}/primary`);
}

export function deletePaymentMethod(id: string): Promise<unknown> {
  return apiDelete(`/api/payments/methods/${id}`);
}

// Legacy in-browser charge simulation. Real checkout now goes through
// api/escrow.ts (booking → escrow initiate → hosted Paystack page); these
// remain only for mock-backed surfaces that haven't moved yet.
export function initiatePayment(input: InitiatePaymentInput): Promise<InitiateResult> {
  return mockResponse(storeInitiate(input));
}

export function verifyPayment(reference: string): Promise<Transaction> {
  return mockResponse(storeVerify(reference));
}

/** Link a settled charge to the booking it created. */
export function attachBooking(reference: string, bookingId: string): void {
  storeAttachBooking(reference, bookingId);
}

export type { InitiatePaymentInput, InitiateResult, MomoNetwork };
