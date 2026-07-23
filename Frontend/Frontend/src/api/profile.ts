import { apiGet, apiPost, apiPut, apiUpload } from './client';

// Own-profile endpoints (api/profile). Email is not editable server-side; the
// PUT accepts name/phone/bio/username/preferredLanguage only.

export interface ProfileMeDto {
  id: string;
  fullName: string;
  email: string;
  phone?: string | null;
  role: number;
  isVerified: boolean;
  emailVerified: boolean;
  phoneVerified: boolean;
  tripNestId?: string | null;
  profilePhotoPath?: string | null;
  username?: string | null;
  bio?: string | null;
}

export function getMyProfile(): Promise<ProfileMeDto> {
  return apiGet<ProfileMeDto>('/api/profile/me');
}

export interface UpdateProfileInput {
  fullName?: string;
  phone?: string;
  bio?: string;
  username?: string;
  /** Language: 0 English, 1 Twi, 2 Ga, 3 French. */
  preferredLanguage?: number;
}

export function updateMyProfile(patch: UpdateProfileInput): Promise<unknown> {
  return apiPut('/api/profile/me', patch);
}

/** Returns the stored photo path (servable once Core exposes /uploads). */
export async function uploadProfilePhoto(photo: File): Promise<string> {
  const form = new FormData();
  form.append('photo', photo);
  const res = await apiUpload<{ photoPath: string }>('/api/profile/photo', form);
  return res.photoPath;
}

// ---- Signature (what lands on agreements) --------------------------------------------------

export interface SignatureInfo {
  hasSignature: boolean;
  updatedAt?: string | null;
  /** Changes are guarded by a cooldown; before this date a replacement is refused. */
  editableFrom?: string | null;
}

export function getSignatureInfo(): Promise<SignatureInfo> {
  return apiGet<SignatureInfo>('/api/profile/signature');
}

/**
 * Sets the signature image. The FIRST upload is free; replacing it requires the account password
 * (plus Ghana Card number once identity-verified) and the 30-day cooldown — signatures land on
 * contracts, so changes must be deliberate.
 */
export function uploadSignature(file: File, password?: string, ghanaCardNumber?: string): Promise<unknown> {
  const form = new FormData();
  form.append('signature', file);
  if (password) form.append('password', password);
  if (ghanaCardNumber) form.append('ghanaCardNumber', ghanaCardNumber);
  return apiUpload('/api/profile/signature', form);
}

// ---- Student status (unlocks the student discount) -----------------------------------------

export interface StudentStatus {
  studentEmail?: string | null;
  isVerifiedStudent: boolean;
  verifiedAt?: string | null;
  expiresAt?: string | null;
}

export function getStudentStatus(): Promise<StudentStatus> {
  return apiGet<StudentStatus>('/api/auth/student');
}

/** Emails a code to the academic address (non-academic domains are rejected server-side). */
export function sendStudentOtp(studentEmail: string): Promise<unknown> {
  return apiPost('/api/auth/student/send-otp', { studentEmail });
}

export function verifyStudentOtp(code: string): Promise<unknown> {
  return apiPost('/api/auth/student/verify-otp', { code });
}
