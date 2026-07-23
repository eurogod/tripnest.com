import type { MaintenanceTicket } from '../types';
import { apiGetList, apiPatch, apiPost } from './client';
import { mapMaintenance, type BookingResponseDto, type MaintenanceResponseDto } from './backend';
import { getPropertyById } from './properties';

export async function getMaintenanceTickets(): Promise<MaintenanceTicket[]> {
  const dtos = await apiGetList<MaintenanceResponseDto>('/api/maintenance/mine');
  return Promise.all(
    dtos.map(async (dto) => mapMaintenance(dto, await getPropertyById(dto.propertyId))),
  );
}

export async function createMaintenanceTicket(
  input: Pick<MaintenanceTicket, 'title' | 'category'>,
): Promise<MaintenanceTicket> {
  // The API requires a propertyId; report against the most recent booking.
  const bookings = await apiGetList<BookingResponseDto>('/api/bookings/user/my-bookings');
  const target = bookings.find((b) => b.status !== 4); // any non-cancelled booking
  if (!target) {
    throw new Error('You need an active booking before reporting a maintenance issue.');
  }
  const dto = await apiPost<MaintenanceResponseDto>('/api/maintenance', {
    propertyId: target.propertyId,
    category: input.category,
    description: input.title,
    priority: 'Medium',
  });
  return mapMaintenance(dto, await getPropertyById(dto.propertyId));
}

// ---- Landlord/caretaker processing ----------------------------------------------------------

export const MAINTENANCE_STATUSES = ['Reported', 'Assigned', 'InProgress', 'Completed', 'Cancelled'] as const;
export type MaintenanceStatusName = (typeof MAINTENANCE_STATUSES)[number];

/** All requests on one property (landlord or assigned caretaker only). */
export function getPropertyMaintenance(propertyId: string): Promise<MaintenanceResponseDto[]> {
  return apiGetList<MaintenanceResponseDto>(`/api/maintenance/property/${propertyId}`);
}

export function updateMaintenanceStatus(id: string, status: MaintenanceStatusName): Promise<unknown> {
  return apiPatch(`/api/maintenance/${id}/status`, { status });
}

/** Hands the issue to a caretaker as a service request (optionally a specific one). */
export function convertToServiceRequest(id: string, caretakerId?: string): Promise<unknown> {
  return apiPost(`/api/maintenance/${id}/convert-to-service-request`, { caretakerId });
}
