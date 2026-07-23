import { apiGet } from './client';

/** Composite trust score (0–100) with a label ("Excellent"…) and trend ("rising"…). */
export interface TrustScore {
  subjectId: string;
  subjectType: string;
  finalScore: number;
  trend: string;
  label: string;
  verificationComponent: number;
  historyComponent: number;
}

export function getPropertyTrustScore(propertyId: string): Promise<TrustScore> {
  return apiGet<TrustScore>(`/api/trustscore/property/${propertyId}`);
}

export function getUserTrustScore(userId: string): Promise<TrustScore> {
  return apiGet<TrustScore>(`/api/trustscore/user/${userId}`);
}
