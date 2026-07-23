import type { HostTask } from '../types';
import { apiDelete, apiGet, apiPatch, apiPost } from './client';
import { mapHostTask, type HostTaskResponseDto, type PagedResultDto } from './backend';
import { taskStatusToApi } from '../lib/enums';
import { getListings } from './listings';

// Host operational tasks, backed by TripNest.Core's /api/tasks. Property ids
// on tasks are joined against the landlord's listings for display titles.

export async function getHostTasks(): Promise<HostTask[]> {
  const [page, listings] = await Promise.all([
    apiGet<PagedResultDto<HostTaskResponseDto>>('/api/tasks?page=1&pageSize=50'),
    getListings().catch(() => []),
  ]);
  const titleById = new Map(listings.map((l) => [l.id, l.title]));
  return page.items.map((dto) => mapHostTask(dto, dto.propertyId ? titleById.get(dto.propertyId) : undefined));
}

export function createHostTask(input: {
  title: string;
  propertyId?: string;
  type?: string;
  priority?: string;
  dueDate?: string;
  assignee?: string;
}): Promise<HostTaskResponseDto> {
  return apiPost<HostTaskResponseDto>('/api/tasks', input);
}

/** Move a task between Todo / InProgress / Done (parsed case-insensitively). */
export function setHostTaskStatus(id: string, status: HostTask['status']): Promise<unknown> {
  return apiPatch(`/api/tasks/${id}`, { status: taskStatusToApi(status) });
}

export function deleteHostTask(id: string): Promise<unknown> {
  return apiDelete(`/api/tasks/${id}`);
}
