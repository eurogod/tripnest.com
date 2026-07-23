import { ApiError, apiGet, apiGetList, apiPatch, apiPut } from './client';

// Agent workspace endpoints: viewing requests assigned to the caller and the
// caller's public directory profile (404 until first saved via PUT).

export interface ViewingRequestDto {
  viewingRequestId: string;
  agentId: string;
  tenantId: string;
  propertyId: string;
  scheduledAt: string;
  notes?: string | null;
  status: string; // "Pending" | "Confirmed" | "Cancelled" | "Completed"
  createdAt: string;
}

export function getMyViewingRequests(): Promise<ViewingRequestDto[]> {
  return apiGetList<ViewingRequestDto>('/api/agents/viewing-requests/mine');
}

/** The server Enum.TryParses this, so the PascalCase names are the contract. */
export type ViewingRequestStatus = 'Confirmed' | 'Cancelled' | 'Completed';

export function updateViewingRequestStatus(id: string, status: ViewingRequestStatus): Promise<unknown> {
  return apiPatch(`/api/agents/viewing-requests/${id}/status`, { status });
}

export const AGENT_STATUS_LABELS = ['Active', 'Inactive', 'Suspended'] as const;

export interface AgentProfileDto {
  agentId: string;
  userId: string;
  licenseNumber: string;
  phoneNumber: string;
  bio: string;
  status: number; // index into AGENT_STATUS_LABELS
  commissionRate?: number | null;
  yearsOfExperience?: number | null;
  joinDate: string;
  certifications?: string | null;
}

/** Null when the agent hasn't created a directory profile yet. */
export async function getMyAgentProfile(): Promise<AgentProfileDto | null> {
  try {
    return await apiGet<AgentProfileDto>('/api/agents/me');
  } catch (e) {
    if (e instanceof ApiError && e.statusCode === 404) return null;
    throw e;
  }
}

export interface UpsertAgentProfileInput {
  licenseNumber: string;
  bio: string;
  phoneNumber?: string;
  commissionRate?: number;
  yearsOfExperience?: number;
  certifications?: string;
}

export function upsertMyAgentProfile(input: UpsertAgentProfileInput): Promise<AgentProfileDto> {
  return apiPut<AgentProfileDto>('/api/agents/me', input);
}
