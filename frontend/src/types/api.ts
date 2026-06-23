// TypeScript mirrors of TripNest.Core DTOs. JSON keys are camelCase; enums are integers.

export interface ApiResponse<T> {
  message: string;
  statusCode: number;
  data: T | null;
  success: boolean;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface LoginResponse {
  userId: string;
  fullName: string;
  email: string;
  role: number;
  accessToken: string;
  refreshToken: string;
  isVerified: boolean;
  emailVerified: boolean;
  phoneVerified: boolean;
  tripNestId?: string | null;
}

export interface UserProfile {
  userId: string;
  fullName: string;
  email: string;
  role: number;
  isVerified: boolean;
  emailVerified: boolean;
  phoneVerified: boolean;
  tripNestId?: string | null;
}

export interface RegisterRequest {
  fullName: string;
  email: string;
  password: string;
  confirmPassword: string;
  phone: string;
  role: number;
}

export interface Property {
  propertyId: string;
  title: string;
  description: string;
  location: string;
  latitude: number;
  longitude: number;
  bedrooms: number;
  bathrooms: number;
  monthlyRent: number;
  dailyRate?: number | null;
  propertyType: string;
  stayType: number;
  cancellationPolicy: number;
  amenities?: string | null;
  photoPaths?: string | null;
  status: number;
  createdAt: string;
  updatedAt: string;
}

export interface CreatePropertyRequest {
  title: string;
  description: string;
  location: string;
  latitude: number;
  longitude: number;
  bedrooms: number;
  bathrooms: number;
  monthlyRent: number;
  dailyRate?: number | null;
  propertyType: string;
  stayType: number;
  cancellationPolicy: number;
  amenities?: string | null;
}

export interface TrustScore {
  subjectId: string;
  subjectType: string;
  finalScore: number;
  trend: string;
  label: string;
  verificationComponent: number;
  historyComponent: number;
  feedbackComponent: number;
}

export interface Review {
  reviewId: string;
  reviewerId: string;
  revieweeId: string;
  propertyId: string;
  rating: number;
  comment: string;
  type: number;
  createdAt: string;
}

export interface Agent {
  agentId: string;
  userId: string;
  licenseNumber: string;
  phoneNumber: string;
  bio: string;
  status: number;
  commissionRate?: number | null;
  yearsOfExperience?: number | null;
  joinDate: string;
  certifications?: string | null;
}

export interface Caretaker {
  caretakerId: string;
  userId: string;
  propertyId: string;
  status: number;
  startDate: string;
  endDate?: string | null;
  monthlyCompensation?: number | null;
  responsibilities: string;
}

export interface Booking {
  bookingId: string;
  propertyId: string;
  checkInDate: string;
  checkOutDate: string;
  totalAmount: number;
  status: number;
  createdAt: string;
}

export interface CreateBookingRequest {
  propertyId: string;
  checkInDate: string;
  checkOutDate: string;
}

export interface Escrow {
  escrowId: string;
  bookingId: string;
  amount: number;
  status: number;
  createdAt: string;
  heldAt?: string | null;
  releasedAt?: string | null;
  releaseReason?: string | null;
  paymentReference?: string | null;
  checkoutUrl?: string | null;
}

export interface Agreement {
  agreementId: string;
  bookingId: string;
  termsContent: string;
  status: number;
  createdAt: string;
  signedAt?: string | null;
  tenantSignature?: string | null;
  landlordSignature?: string | null;
  expiryDate?: string | null;
}

export interface Notification {
  notificationId: string;
  userId: string;
  title: string;
  message: string;
  isRead: boolean;
  relatedEntityId?: string | null;
  relatedEntityType?: string | null;
  createdAt: string;
  readAt?: string | null;
}

export interface CommunicationPreference {
  userId: string;
  smsEnabled: boolean;
  emailEnabled: boolean;
}

export interface Receipt {
  receiptId: string;
  bookingId: string;
  userId: string;
  amount: number;
  description?: string | null;
  paymentMethod?: string | null;
  createdAt: string;
}

export interface VerificationStatusResponse {
  verificationId: string;
  ghanaCardNumber: string;
  status: number;
  faceMatchScore?: number | null;
  failureReason?: string | null;
  submittedAt: string;
  reviewedAt?: string | null;
}

export interface StartVerificationRequest {
  ghanaCardNumber: string;
  selfiePhotoPath: string;
  firstName: string;
  lastName: string;
  dateOfBirth: string; // yyyy-MM-dd
}

export interface MaintenanceRequest {
  maintenanceId: string;
  propertyId: string;
  reportedByUserId: string;
  description: string;
  status: number;
  photoPath?: string | null;
  createdAt: string;
  completedAt?: string | null;
  resolution?: string | null;
}

export interface ServiceRequest {
  serviceRequestId: string;
  caretakerId: string;
  requestedByUserId: string;
  propertyId: string;
  serviceType: string;
  description: string;
  status: string;
  rating?: number | null;
  reviewComment?: string | null;
  createdAt: string;
  completedAt?: string | null;
}

export interface ViewingRequest {
  viewingRequestId: string;
  agentId: string;
  tenantId: string;
  propertyId: string;
  scheduledAt: string;
  notes?: string | null;
  status: string;
  createdAt: string;
}

export interface Conversation {
  conversationId: string;
  user1Id: string;
  user2Id: string;
  propertyId?: string | null;
  createdAt: string;
  lastMessageAt?: string | null;
}

export interface Message {
  messageId: string;
  conversationId: string;
  senderId: string;
  content: string;
  type: number;
  createdAt: string;
  isRead: boolean;
  readAt?: string | null;
}

export interface Walkthrough {
  walkthroughId: string;
  propertyId: string;
  title: string;
  videoPath: string;
  thumbnailUrl?: string | null;
  durationSeconds: number;
  createdAt: string;
}

export interface TrustedContact {
  name?: string | null;
  phone?: string | null;
  email?: string | null;
}

export interface GlobalSearchResult {
  id: string;
  type: string;
  title: string;
  subtitle: string;
  thumbnailUrl?: string | null;
}

export interface MapConfig {
  provider: string;
  tileUrl: string;
  attribution: string;
  maxZoom: number;
}

export interface AppConfig {
  appName: string;
  stayTypes: string[];
  serviceTypes: string[];
  maintenanceCategories: string[];
  map: MapConfig;
}

export interface AdminStats {
  totalUsers: number;
  totalTenants: number;
  totalLandlords: number;
  totalAgents: number;
  totalCaretakers: number;
  verifiedUsers: number;
  pendingVerifications: number;
  totalProperties: number;
  activeProperties: number;
  pendingWalkthroughs: number;
  totalBookings: number;
  confirmedBookings: number;
  completedBookings: number;
  cancelledBookings: number;
  totalEscrowHeld: number;
  totalEscrowReleased: number;
  openDisputes: number;
  openMaintenanceRequests: number;
  activeServiceRequests: number;
  averageTrustScore: number;
}
