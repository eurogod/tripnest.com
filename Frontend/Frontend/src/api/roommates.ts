import { apiDelete, apiGet, apiGetList, apiPut } from './client';

// Roommate matching for students/long-term tenants: publish a preferences profile, get scored
// matches (hard conflicts like smoking intolerance are filtered out, not scored low), and an
// AI explanation of why a match works.

export interface RoommateProfileInput {
  bio?: string;
  university?: string;
  preferredLocation: string;
  monthlyBudget: number;
  moveInDate?: string;
  smokes: boolean;
  okWithSmoker: boolean;
  hasPets: boolean;
  okWithPets: boolean;
  nightOwl: boolean;
  cleanlinessLevel: number; // 1–5
  isVisible: boolean;
}

export interface RoommateProfile extends RoommateProfileInput {
  userId: string;
  fullName?: string | null;
  /** Ghana Card identity verification — the trust signal for moving in with a stranger. */
  isVerified: boolean;
}

export interface RoommateMatch {
  profile: RoommateProfile;
  /** 0–100 compatibility across budget, location, university and habits. */
  score: number;
}

export function getMyRoommateProfile(): Promise<RoommateProfile> {
  return apiGet<RoommateProfile>('/api/roommates/me');
}

export function upsertRoommateProfile(input: RoommateProfileInput): Promise<RoommateProfile> {
  return apiPut<RoommateProfile>('/api/roommates/me', input);
}

/** Unlists the profile entirely (matches stop immediately). */
export function deleteRoommateProfile(): Promise<unknown> {
  return apiDelete('/api/roommates/me');
}

export function getRoommateMatches(): Promise<RoommateMatch[]> {
  return apiGetList<RoommateMatch>('/api/roommates/matches');
}

export interface MatchExplanation {
  explanation: string;
  sharedTraits: string[];
  considerations: string[];
}

export function getMatchExplanation(otherUserId: string): Promise<MatchExplanation> {
  return apiGet<MatchExplanation>(`/api/roommates/matches/${otherUserId}/explanation`);
}
