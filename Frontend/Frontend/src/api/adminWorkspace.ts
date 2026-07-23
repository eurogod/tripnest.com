import { apiGetList, apiPatch, apiPost } from './client';
import type { EscrowResponseDto } from './escrow';

// Admin workspace: dispute queue, assistant-escalation support tickets and
// the audit trail. All Admin-role-only server-side.

export function getDisputedEscrows(): Promise<EscrowResponseDto[]> {
  return apiGetList<EscrowResponseDto>('/api/escrow/disputes');
}

/** approved=true releases the funds to the landlord; false refunds the tenant. */
export function resolveDispute(escrowId: string, approved: boolean): Promise<unknown> {
  return apiPatch(`/api/escrow/${escrowId}/resolve-dispute`, { approved });
}

export function refundEscrow(escrowId: string, reason: string): Promise<unknown> {
  return apiPost(`/api/escrow/${escrowId}/refund`, { reason });
}

export const TICKET_STATUS_LABELS = ['Open', 'Resolved'] as const;

export interface SupportTicketDto {
  ticketId: string;
  userId: string;
  userName?: string | null;
  userEmail?: string | null;
  subject: string;
  summary: string;
  status: number; // index into TICKET_STATUS_LABELS
  conversationId?: string | null;
  createdAt: string;
  resolvedAt?: string | null;
}

export function getSupportTickets(): Promise<SupportTicketDto[]> {
  return apiGetList<SupportTicketDto>('/api/admin/support-tickets');
}

export function resolveSupportTicket(ticketId: string): Promise<unknown> {
  return apiPost(`/api/admin/support-tickets/${ticketId}/resolve`);
}

export interface AuditLogDto {
  id: string;
  userId: string;
  action: string;
  entityType: string;
  entityId: string;
  oldValue?: string | null;
  newValue?: string | null;
  createdAt: string;
  ipAddress?: string | null;
}

export function getAuditLogs(limit = 100): Promise<AuditLogDto[]> {
  return apiGetList<AuditLogDto>(`/api/admin/audit-logs?limit=${limit}`);
}
