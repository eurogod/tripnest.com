// TripNest.Core serializes enums as integers. These tables translate between
// the wire integers and the string unions the UI components use.
// Source of truth: TripNest.Core/Enums/*.cs — keep in sync.

import type { Role } from '../store/authStore';

// UserRole: Tenant, Landlord, Agent, Caretaker, Admin, Guest
const ROLES: Role[] = ['tenant', 'landlord', 'agent', 'caretaker', 'admin', 'guest'];
export const roleFromInt = (n: number): Role => ROLES[n] ?? 'guest';
export const roleToInt = (r: Role): number => Math.max(0, ROLES.indexOf(r));

// BookingStatus: Pending, Confirmed, CheckedIn, CheckedOut, Cancelled, Completed
export type BookingStatusName =
  | 'pending' | 'confirmed' | 'checked-in' | 'checked-out' | 'cancelled' | 'completed';
const BOOKING_STATUS: BookingStatusName[] =
  ['pending', 'confirmed', 'checked-in', 'checked-out', 'cancelled', 'completed'];
export const bookingStatusFromInt = (n: number): BookingStatusName => BOOKING_STATUS[n] ?? 'pending';

/** Collapse the backend's 6 booking states into the tenant UI's 4. */
export function bookingStatusToUi(n: number, checkOut: string | Date): 'upcoming' | 'active' | 'past' | 'cancelled' {
  const s = bookingStatusFromInt(n);
  if (s === 'cancelled') return 'cancelled';
  if (s === 'checked-in') return 'active';
  if (s === 'completed' || s === 'checked-out') return 'past';
  return new Date(checkOut) < new Date() ? 'past' : 'upcoming';
}

// MaintenanceStatus: Reported, Assigned, InProgress, Completed, Cancelled
export function maintenanceStatusToUi(n: number): 'pending' | 'in-progress' | 'resolved' {
  if (n === 3) return 'resolved';
  if (n === 1 || n === 2) return 'in-progress';
  return 'pending';
}

// AgreementStatus: Draft, Pending, Signed, Expired, Terminated.
// Terminated stays distinct from Expired — ended deliberately vs simply ran out.
const AGREEMENT_STATUSES = ['draft', 'pending', 'active', 'expired', 'terminated'] as const;
export function agreementStatusToUi(n: number): (typeof AGREEMENT_STATUSES)[number] {
  return AGREEMENT_STATUSES[n] ?? 'pending';
}

// InquiryStatus: New, Replied, Archived
const INQUIRY_STATUS = ['new', 'replied', 'archived'] as const;
export const inquiryStatusFromInt = (n: number) => INQUIRY_STATUS[n] ?? 'new';

// HostTask enums — requests accept string names; responses return ints.
const TASK_TYPES = ['cleaning', 'maintenance', 'inspection', 'restock'] as const;
const TASK_PRIORITIES = ['low', 'medium', 'high'] as const;
const TASK_STATUSES = ['todo', 'in-progress', 'done'] as const;
export const taskTypeFromInt = (n: number) => TASK_TYPES[n] ?? 'maintenance';
export const taskPriorityFromInt = (n: number) => TASK_PRIORITIES[n] ?? 'medium';
export const taskStatusFromInt = (n: number) => TASK_STATUSES[n] ?? 'todo';
/** Backend enum names have no dash: in-progress → InProgress. */
export const taskStatusToApi = (s: string) => s.replace('-', '');

// TeamMemberRole: Owner, CoHost, Cleaner, Maintenance, Agent · TeamMemberStatus: Active, Invited, Suspended
const TEAM_ROLES = ['owner', 'co-host', 'cleaner', 'maintenance', 'agent'] as const;
const TEAM_STATUSES = ['active', 'invited', 'suspended'] as const;
export const teamRoleFromInt = (n: number) => TEAM_ROLES[n] ?? 'co-host';
export const teamStatusFromInt = (n: number) => TEAM_STATUSES[n] ?? 'invited';
export const teamRoleToApi = (s: string) => s.replace('-', '');

// ExchangeCategory: Tips, Suppliers, Regulation, Marketplace, General (UI uses capitalized labels)
const EXCHANGE_CATEGORIES = ['Tips', 'Suppliers', 'Regulation', 'Marketplace', 'General'] as const;
export const exchangeCategoryFromInt = (n: number) => EXCHANGE_CATEGORIES[n] ?? 'General';

// ResourceCategory: Guide, Policy, Template, Video
const RESOURCE_CATEGORIES = ['guide', 'policy', 'template', 'video'] as const;
export const resourceCategoryFromInt = (n: number) => RESOURCE_CATEGORIES[n] ?? 'guide';

// StatementStatus: Pending, Paid
export const statementStatusFromInt = (n: number): 'pending' | 'paid' => (n === 1 ? 'paid' : 'pending');

// PropertyStatus: Draft, Active, Inactive, Archived → UI listing status
export function listingStatusFromInt(n: number): 'published' | 'unlisted' | 'draft' | 'snoozed' {
  if (n === 1) return 'published';
  if (n === 2) return 'unlisted';
  if (n === 3) return 'snoozed';
  return 'draft';
}

/** "kofi mensah" → "KM" for avatar chips. */
export function initialsOf(name: string | null | undefined): string {
  return (name ?? '')
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0].toUpperCase())
    .join('') || '?';
}

/** Split the backend's delimited string fields (amenities, photoPaths). */
export function splitList(value: string | null | undefined): string[] {
  return (value ?? '')
    .split(/[;,|]/)
    .map((s) => s.trim())
    .filter(Boolean);
}
