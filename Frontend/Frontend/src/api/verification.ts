import { ApiError, apiGet, apiPost, apiUpload } from './client';
import type { VerificationStatusResponseDto } from './backend';

// ---------------------------------------------------------------------------
// Ghana Card identity verification (api/verification). The flow is async on
// the server: `start` queues the request and returns Pending immediately; a
// background worker resolves it to Verified/Rejected, so callers poll
// `getVerificationStatus` for the outcome.
// ---------------------------------------------------------------------------

export type VerificationState = 'not-started' | 'pending' | 'verified' | 'rejected';

export interface VerificationStatus {
  state: VerificationState;
  ghanaCardNumber: string;
  failureReason: string | null;
  submittedAt: string;
  reviewedAt: string | null;
}

const STATE_BY_CODE: Record<number, VerificationState> = {
  0: 'not-started',
  1: 'pending',
  2: 'verified',
  3: 'rejected',
};

function mapStatus(dto: VerificationStatusResponseDto): VerificationStatus {
  return {
    state: STATE_BY_CODE[dto.status] ?? 'not-started',
    ghanaCardNumber: dto.ghanaCardNumber,
    failureReason: dto.failureReason ?? null,
    submittedAt: dto.submittedAt,
    reviewedAt: dto.reviewedAt ?? null,
  };
}

/** The caller's latest verification attempt; null when they never submitted one. */
export async function getVerificationStatus(): Promise<VerificationStatus | null> {
  try {
    return mapStatus(await apiGet<VerificationStatusResponseDto>('/api/verification/status'));
  } catch (err) {
    // The backend 400s ("No verification request found") for first-time users.
    if (err instanceof ApiError && err.statusCode === 400) return null;
    throw err;
  }
}

export interface StartVerificationInput {
  ghanaCardNumber: string;
  firstName: string;
  lastName: string;
  /** YYYY-MM-DD, as bound by the backend's DateOnly. */
  dateOfBirth: string;
  selfie: File;
}

// ---------------------------------------------------------------------------
// Email / phone contact verification (api/auth): a single-use 6-digit code is
// sent to the account's own address/number; verifying flips the profile flag.
// ---------------------------------------------------------------------------

export const sendEmailOtp = () => apiPost('/api/auth/email/send-otp');
export const verifyEmailOtp = (code: string) => apiPost('/api/auth/email/verify-otp', { code });
export const sendPhoneOtp = () => apiPost('/api/auth/phone/send-otp');
export const verifyPhoneOtp = (code: string) => apiPost('/api/auth/phone/verify-otp', { code });

/**
 * Uploads the selfie, then queues the identity check. Returns Pending on
 * success (or the existing Pending attempt if one is already in flight).
 */
export async function startVerification(input: StartVerificationInput): Promise<VerificationStatus> {
  const form = new FormData();
  form.append('selfie', input.selfie);
  const upload = await apiUpload<{ selfiePhotoPath: string }>('/api/verification/selfie', form);

  const dto = await apiPost<VerificationStatusResponseDto>('/api/verification/start', {
    ghanaCardNumber: input.ghanaCardNumber,
    selfiePhotoPath: upload.selfiePhotoPath,
    firstName: input.firstName,
    lastName: input.lastName,
    dateOfBirth: input.dateOfBirth,
  });
  return mapStatus(dto);
}
