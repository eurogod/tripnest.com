import { apiDelete, apiGetList, apiPatch, apiUpload } from './client';

// Walkthrough video endpoints on TripNest.Core. Quirks the UI must respect:
// video bytes are unreachable today (no static serving / streaming endpoint),
// thumbnailUrl and durationSeconds are never populated, and review status is
// Agent/Admin-only — landlords can only see that videos were submitted.

export interface WalkthroughResponseDto {
  walkthroughId: string;
  propertyId: string;
  title: string;
  videoPath: string; // dead path until Core serves /uploads
  thumbnailUrl: string | null; // always null
  durationSeconds: number; // always 0
  createdAt: string;
}

export function getWalkthroughs(propertyId: string): Promise<WalkthroughResponseDto[]> {
  return apiGetList<WalkthroughResponseDto>(`/api/properties/${propertyId}/walkthroughs`);
}

/** Upload a generated clip for review (owner only). */
export function uploadWalkthrough(
  propertyId: string,
  title: string,
  video: Blob,
): Promise<WalkthroughResponseDto> {
  const form = new FormData();
  form.append('title', title);
  // Extension must match the backend's allowlist (.mp4/.webm both pass).
  const webm = video.type.includes('webm');
  form.append('videoFile', new File(
    [video],
    webm ? 'walkthrough.webm' : 'walkthrough.mp4',
    { type: video.type || 'video/mp4' },
  ));
  return apiUpload<WalkthroughResponseDto>(`/api/properties/${propertyId}/walkthrough`, form);
}

// Mirror the backend's UploadValidation allowlist so bad picks fail before the upload.
export const WALKTHROUGH_VIDEO_EXTENSIONS = ['.mp4', '.mov', '.avi', '.webm', '.mkv'] as const;
export const WALKTHROUGH_VIDEO_MAX_BYTES = 100 * 1024 * 1024; // 100 MB

/**
 * Upload a landlord-recorded walkthrough video for a property (owner only).
 * Keeps the file's own name — the backend validates the extension against its
 * allowlist and checks the bytes match, so renaming would get .mov/.avi/.mkv
 * files rejected. Sets the property's walkthrough to Pending review; an
 * Agent/Admin must approve it before the property can go Active.
 */
export function uploadWalkthroughFile(
  propertyId: string,
  title: string,
  file: File,
): Promise<WalkthroughResponseDto> {
  const form = new FormData();
  form.append('title', title);
  form.append('videoFile', file, file.name);
  return apiUpload<WalkthroughResponseDto>(`/api/properties/${propertyId}/walkthrough`, form);
}

export function deleteWalkthrough(propertyId: string, walkthroughId: string): Promise<unknown> {
  return apiDelete(`/api/properties/${propertyId}/walkthroughs/${walkthroughId}`);
}

// --- Review queue (Agent/Admin) ---

/** WalkthroughStatus enum, serialized as a number like every backend enum. */
export const WALKTHROUGH_STATUS_LABELS = ['Not submitted', 'Pending review', 'Approved', 'Rejected'] as const;

export interface PropertyWalkthroughStatusDto {
  propertyId: string;
  walkthroughStatus: number; // index into WALKTHROUGH_STATUS_LABELS
  videoPath?: string | null;
  rejectionReason?: string | null;
  reviewedAt?: string | null;
}

export function getPendingWalkthroughs(): Promise<PropertyWalkthroughStatusDto[]> {
  return apiGetList<PropertyWalkthroughStatusDto>('/api/properties/pending-walkthroughs');
}

export function reviewWalkthrough(
  propertyId: string,
  approved: boolean,
  rejectionReason?: string,
): Promise<unknown> {
  return apiPatch(`/api/properties/${propertyId}/walkthrough/review`, { approved, rejectionReason });
}
