import { apiGet, apiPost, apiPut } from './client';

// Safety module (api/safety): a trusted contact on file, safe-arrival
// check-ins that notify them (dev logs the SMS), the emergency alert that
// bypasses notification opt-outs, and the 24/7 urgent-help line that pages
// every admin.

export interface TrustedContact {
  name?: string | null;
  phone?: string | null;
  email?: string | null;
}

export const getTrustedContact = () => apiGet<TrustedContact>('/api/safety/contact');

/** Phone is normalised to E.164 server-side; invalid numbers 400. */
export const saveTrustedContact = (contact: TrustedContact) =>
  apiPut<TrustedContact>('/api/safety/contact', contact);

export interface SafetyCheckInResult {
  checkInId: string;
  bookingId: string;
  isCheckedIn: boolean;
  checkedInAt?: string | null;
  contactNotified: boolean;
  locationShared: boolean;
}

/** Safe-arrival check-in for a booking; coordinates only sent with consent. */
export const safetyCheckIn = (input: {
  bookingId: string;
  shareLocation: boolean;
  latitude?: number;
  longitude?: number;
  /** Per-request contact override; the saved trusted contact is the default. */
  contactPhone?: string;
}) => apiPost<SafetyCheckInResult>('/api/safety/checkin', input);

/** Emergency alert: notifies the user (ignoring opt-outs) and the trusted contact. */
export const sendEmergencyAlert = (bookingId: string) =>
  apiPost('/api/safety/alert', { bookingId });

export interface UrgentHelpResult {
  hotline?: string | null;
  promisedResponseMinutes: number;
}

/**
 * Locked out / unsafe RIGHT NOW: files a queue-jumping urgent ticket, pages every admin over the
 * emergency channel, and returns the 24/7 hotline number to call.
 */
export const requestUrgentHelp = (message: string) =>
  apiPost<UrgentHelpResult>('/api/safety/urgent', { message });
