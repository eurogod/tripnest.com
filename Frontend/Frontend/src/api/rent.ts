import { apiGetList, apiPost } from './client';

// Monthly rent for long-term/student stays: the upfront charge covers period 1; periods 2..N
// arrive as invoices the tenant pays month by month.

export const RENT_STATUS = ['Upcoming', 'Due', 'Paid', 'Overdue', 'Cancelled'] as const;

export interface RentInvoiceDto {
  invoiceId: string;
  bookingId: string;
  propertyId: string;
  periodStart: string;
  periodEnd: string;
  amount: number;
  dueDate: string;
  status: number; // index into RENT_STATUS
  paidAt?: string | null;
  /** Set only on the pay response — the tenant's checkout link. */
  checkoutUrl?: string | null;
}

export function getMyRentInvoices(): Promise<RentInvoiceDto[]> {
  return apiGetList<RentInvoiceDto>('/api/rent/mine');
}

/** Starts payment; the response carries the gateway checkout link to redirect to. */
export function payRentInvoice(invoiceId: string): Promise<RentInvoiceDto> {
  return apiPost<RentInvoiceDto>(`/api/rent/invoices/${invoiceId}/pay`);
}

/** Confirms the payment with the gateway (called after checkout returns; idempotent). */
export function verifyRentInvoice(invoiceId: string): Promise<RentInvoiceDto> {
  return apiPost<RentInvoiceDto>(`/api/rent/invoices/${invoiceId}/verify`);
}
