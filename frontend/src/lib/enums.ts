// Backend enums serialize as INTEGERS (no JsonStringEnumConverter registered).
// These maps mirror the C# enum declaration order in TripNest.Core/Enums.

export enum UserRole {
  Tenant = 0,
  Landlord = 1,
  Agent = 2,
  Caretaker = 3,
  Admin = 4,
  Guest = 5,
}
export const UserRoleLabel: Record<number, string> = {
  0: 'Tenant',
  1: 'Landlord',
  2: 'Agent',
  3: 'Caretaker',
  4: 'Admin',
  5: 'Guest',
};

export enum VerificationStatus {
  NotStarted = 0,
  Pending = 1,
  Verified = 2,
  Rejected = 3,
}

export enum StayType {
  ShortTerm = 0,
  LongTerm = 1,
  Student = 2,
}
export const StayTypeLabel: Record<number, string> = {
  0: 'Short stay',
  1: 'Long-term',
  2: 'Student',
};

export enum CancellationPolicy {
  Flexible = 0,
  Moderate = 1,
  Strict = 2,
}
export const CancellationPolicyLabel: Record<number, string> = {
  0: 'Flexible',
  1: 'Moderate',
  2: 'Strict',
};

export enum PropertyStatus {
  Draft = 0,
  Active = 1,
  Inactive = 2,
  Archived = 3,
}
export const PropertyStatusLabel: Record<number, string> = {
  0: 'Draft',
  1: 'Active',
  2: 'Inactive',
  3: 'Archived',
};

export enum BookingStatus {
  Pending = 0,
  Confirmed = 1,
  CheckedIn = 2,
  CheckedOut = 3,
  Cancelled = 4,
  Completed = 5,
}
export const BookingStatusLabel: Record<number, string> = {
  0: 'Pending',
  1: 'Confirmed',
  2: 'Checked in',
  3: 'Checked out',
  4: 'Cancelled',
  5: 'Completed',
};

export enum EscrowStatus {
  Pending = 0,
  HeldInEscrow = 1,
  Released = 2,
  Refunded = 3,
  Disputed = 4,
}
export const EscrowStatusLabel: Record<number, string> = {
  0: 'Pending',
  1: 'Held in escrow',
  2: 'Released',
  3: 'Refunded',
  4: 'Disputed',
};

export enum AgreementStatus {
  Draft = 0,
  Pending = 1,
  Signed = 2,
  Expired = 3,
  Terminated = 4,
}
export const AgreementStatusLabel: Record<number, string> = {
  0: 'Draft',
  1: 'Pending signature',
  2: 'Signed',
  3: 'Expired',
  4: 'Terminated',
};

export enum WalkthroughStatus {
  NotSubmitted = 0,
  PendingReview = 1,
  Approved = 2,
  Rejected = 3,
}
export const WalkthroughStatusLabel: Record<number, string> = {
  0: 'Not submitted',
  1: 'Pending review',
  2: 'Approved',
  3: 'Rejected',
};

export enum ReviewType {
  Property = 0,
  Tenant = 1,
  Landlord = 2,
}

export enum AgentStatus {
  Active = 0,
  Inactive = 1,
  Suspended = 2,
}

export enum CaretakerStatus {
  Active = 0,
  Inactive = 1,
  Suspended = 2,
}

export enum NotificationType {
  BookingConfirmed = 0,
  PaymentReceived = 1,
  AgreementReady = 2,
  MaintenanceUpdate = 3,
  ServiceRequestUpdate = 4,
  SafetyAlert = 5,
  VerificationStatusChanged = 6,
  General = 7,
}

export enum MessageType {
  Text = 0,
  Image = 1,
  File = 2,
  Document = 3,
}

export enum MaintenanceStatus {
  Reported = 0,
  Assigned = 1,
  InProgress = 2,
  Completed = 3,
  Cancelled = 4,
}
export const MaintenanceStatusLabel: Record<number, string> = {
  0: 'Reported',
  1: 'Assigned',
  2: 'In progress',
  3: 'Completed',
  4: 'Cancelled',
};

// ServiceRequest.Status and ViewingRequest.Status are serialized by the backend as STRINGS.
export type ServiceRequestStatus = 'Pending' | 'Accepted' | 'InProgress' | 'Completed' | 'Cancelled';
export type ViewingRequestStatusStr = 'Pending' | 'Confirmed' | 'Cancelled' | 'Completed';
