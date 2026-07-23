import type { TeamRole, TeamUser, TeamUserStatus } from '../types';
import { apiDelete, apiGetList, apiPatch, apiPost } from './client';
import { mapTeamMember, type TeamMemberResponseDto } from './backend';
import { teamRoleToApi } from '../lib/enums';

// Host team management, backed by TripNest.Core's /api/team.

export async function getTeamUsers(): Promise<TeamUser[]> {
  const dtos = await apiGetList<TeamMemberResponseDto>('/api/team');
  return dtos.map(mapTeamMember);
}

export async function inviteTeamUser(name: string, email: string, role: TeamRole): Promise<TeamUser> {
  const dto = await apiPost<TeamMemberResponseDto>('/api/team', {
    name,
    email,
    // Backend parses enum names case-insensitively; only the dash must go.
    role: teamRoleToApi(role),
    propertiesCount: 0,
  });
  return mapTeamMember(dto);
}

export function setTeamUserStatus(id: string, status: TeamUserStatus): Promise<unknown> {
  const name = status.charAt(0).toUpperCase() + status.slice(1);
  return apiPatch(`/api/team/${id}`, { status: name });
}

export function removeTeamUser(id: string): Promise<unknown> {
  return apiDelete(`/api/team/${id}`);
}
