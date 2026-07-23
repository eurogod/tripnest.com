import { apiGet, apiGetList, apiPatch } from './client';

// Caretaker workspace: service requests raised against the caller's
// assignments. GET /mine is role-aware server-side (caretaker sees requests
// on their assignments; requesters see the ones they raised).

export interface ServiceRequestDto {
  serviceRequestId: string;
  caretakerId: string;
  requestedByUserId: string;
  propertyId: string;
  serviceType: string;
  description: string;
  status: string; // "Pending" | "Accepted" | "InProgress" | "Completed" | "Cancelled"
  rating?: number | null;
  reviewComment?: string | null;
  createdAt: string;
  completedAt?: string | null;
}

export function getMyServiceRequests(): Promise<ServiceRequestDto[]> {
  return apiGetList<ServiceRequestDto>('/api/caretakers/service-requests/mine');
}

export function acceptServiceRequest(id: string): Promise<unknown> {
  return apiPatch(`/api/caretakers/service-requests/${id}/accept`);
}

/** The server Enum.TryParses this, so the PascalCase names are the contract. */
export type ServiceRequestStatus = 'InProgress' | 'Completed' | 'Cancelled';

export function updateServiceRequestStatus(id: string, status: ServiceRequestStatus): Promise<unknown> {
  return apiPatch(`/api/caretakers/service-requests/${id}/status`, { status });
}

// ---- Self-service (the caretaker's own profile) ---------------------------------------------

export interface CaretakerProfileDto {
  caretakerId: string;
  userId: string;
  status: number; // 0 Active, 1 Inactive, 2 Suspended
  monthlyCompensation?: number | null;
  responsibilities: string;
  bio?: string | null;
  serviceArea?: string | null;
}

export function getMyCaretakerProfile(): Promise<CaretakerProfileDto> {
  return apiGet<CaretakerProfileDto>('/api/caretakers/me');
}

/** Flip availability: Active (0) takes new work, Inactive (1) pauses new requests. */
export function setMyAvailability(active: boolean): Promise<unknown> {
  return apiPatch('/api/caretakers/me/availability', { status: active ? 0 : 1 });
}
