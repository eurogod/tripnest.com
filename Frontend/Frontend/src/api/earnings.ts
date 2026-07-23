import type { EarningsSummary, EarningStatus, EarningTxn, PayoutAccount } from '../types';
import { ApiError, apiGet, apiGetList, apiPost, apiPut } from './client';
import {
  formatIsoDate,
  type LandlordBookingResponseDto,
  type LandlordEarningsDto,
  type PagedResultDto,
  type PayoutAccountResponseDto,
  type PayoutResponseDto,
} from './backend';

// Landlord earnings: headline totals from /api/landlord/earnings, and the
// transactions table from /api/payouts/mine (one payout per released escrow,
// created automatically — there is no manual "withdraw").

const PAYOUT_STATUS: Record<number, EarningStatus> = {
  0: 'pending',
  1: 'processing',
  2: 'settled', // Paid
  3: 'failed',
};

function mapPayout(dto: PayoutResponseDto, booking?: LandlordBookingResponseDto): EarningTxn {
  return {
    id: dto.payoutId,
    date: formatIsoDate(dto.paidAt ?? dto.createdAt),
    guest: booking?.guest ?? 'Guest',
    listing: booking?.listing ?? `Booking ${dto.bookingId.slice(0, 8).toUpperCase()}`,
    gross: dto.grossAmount,
    fee: dto.feeAmount,
    net: dto.amount,
    status: PAYOUT_STATUS[dto.status] ?? 'pending',
  };
}

export async function getEarnings(): Promise<EarningsSummary> {
  const [dto, payouts, bookings] = await Promise.all([
    apiGet<LandlordEarningsDto>('/api/landlord/earnings'),
    apiGetList<PayoutResponseDto>('/api/payouts/mine').catch(() => [] as PayoutResponseDto[]),
    apiGet<PagedResultDto<LandlordBookingResponseDto>>('/api/landlord/bookings?page=1&pageSize=50')
      .catch(() => null),
  ]);
  const byBooking = new Map((bookings?.items ?? []).map((b) => [b.bookingId, b]));
  const transactions = payouts.map((p) => mapPayout(p, byBooking.get(p.bookingId)));
  const inFlight = payouts
    .filter((p) => p.status === 0 || p.status === 1)
    .reduce((sum, p) => sum + p.amount, 0);

  return {
    available: dto.totalEarnings,
    pending: inFlight,
    thisMonth: dto.thisMonthEarnings,
    lastMonth: dto.lastMonthEarnings,
    lifetime: dto.totalEarnings,
    nextPayoutDate: 'On escrow release',
    transactions,
  };
}

function mapPayoutAccount(dto: PayoutAccountResponseDto): PayoutAccount {
  return {
    channel: dto.channel === 'ghipss' ? 'ghipss' : 'mobile_money',
    providerCode: dto.providerCode,
    accountNumber: dto.accountNumber,
    accountName: dto.accountName,
    providerRegistered: dto.providerRegistered,
  };
}

/** The registered payout destination; null until the host sets one up. */
export async function getPayoutAccount(): Promise<PayoutAccount | null> {
  try {
    return mapPayoutAccount(await apiGet<PayoutAccountResponseDto>('/api/payouts/account'));
  } catch (err) {
    if (err instanceof ApiError && err.statusCode === 404) return null;
    throw err;
  }
}

export interface SavePayoutAccountInput {
  channel: 'mobile_money' | 'ghipss';
  providerCode: string;
  accountNumber: string;
  accountName: string;
}

export async function savePayoutAccount(input: SavePayoutAccountInput): Promise<PayoutAccount> {
  return mapPayoutAccount(await apiPut<PayoutAccountResponseDto>('/api/payouts/account', input));
}

/** Re-attempt a Pending/Failed payout (e.g. after fixing the payout account). */
export async function retryPayout(payoutId: string): Promise<EarningTxn> {
  return mapPayout(await apiPost<PayoutResponseDto>(`/api/payouts/${payoutId}/retry`));
}
