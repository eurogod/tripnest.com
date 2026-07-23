import { apiGet } from './client';

/** Loyalty tier from completed stays; the discount is platform-funded and applies automatically. */
export interface LoyaltyStatus {
  tier: string;
  completedStays: number;
  discountPercent: number;
  nextTier?: string | null;
  staysToNextTier?: number | null;
}

export function getLoyaltyStatus(): Promise<LoyaltyStatus> {
  return apiGet<LoyaltyStatus>('/api/loyalty/me');
}
