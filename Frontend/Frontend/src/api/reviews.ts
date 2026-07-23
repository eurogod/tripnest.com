import { apiPost } from './client';

/**
 * Leave a review on a completed stay. The backend enforces the honest-review rules
 * (must be the stay's tenant, stay completed, one review per booking).
 */
export function submitReview(input: {
  bookingId: string;
  propertyId: string;
  rating: number; // 1–5
  comment?: string;
}): Promise<unknown> {
  return apiPost('/api/reviews', input);
}
