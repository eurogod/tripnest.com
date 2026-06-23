import { api } from './api';
import type {
  AdminStats,
  Agent,
  Agreement,
  AppConfig,
  Booking,
  Caretaker,
  CommunicationPreference,
  Conversation,
  CreateBookingRequest,
  CreatePropertyRequest,
  Escrow,
  GlobalSearchResult,
  LoginResponse,
  MaintenanceRequest,
  Message,
  Notification,
  PagedResult,
  Property,
  Receipt,
  RegisterRequest,
  Review,
  ServiceRequest,
  StartVerificationRequest,
  TrustScore,
  TrustedContact,
  UserProfile,
  VerificationStatusResponse,
  ViewingRequest,
  Walkthrough,
} from '@/types/api';

export const authApi = {
  login: (email: string, password: string) => api.post<LoginResponse>('/auth/login', { email, password }),
  register: (body: RegisterRequest) => api.post<LoginResponse>('/auth/register', body),
  me: () => api.get<UserProfile>('/auth/me'),
  logout: () => api.post<Record<string, never>>('/auth/logout'),
  forgotPassword: (email: string) => api.post<Record<string, never>>('/auth/forgot-password', { email }),
  resetPassword: (b: { email: string; resetToken: string; newPassword: string; confirmPassword: string }) =>
    api.post<Record<string, never>>('/auth/reset-password', b),
  changePassword: (b: { currentPassword: string; newPassword: string; confirmNewPassword: string }) =>
    api.post<Record<string, never>>('/auth/change-password', b),
  sendPhoneOtp: () => api.post<Record<string, never>>('/auth/phone/send-otp'),
  verifyPhoneOtp: (code: string) => api.post<Record<string, never>>('/auth/phone/verify-otp', { code }),
  sendEmailOtp: () => api.post<Record<string, never>>('/auth/email/send-otp'),
  verifyEmailOtp: (code: string) => api.post<Record<string, never>>('/auth/email/verify-otp', { code }),
};

export const configApi = {
  appInfo: () => api.get<AppConfig>('/config/app-info'),
};

export const propertiesApi = {
  list: () => api.get<Property[]>('/properties'),
  get: (id: string) => api.get<Property>(`/properties/${id}`),
  search: (params: { location?: string; minBedrooms?: number; maxBedrooms?: number }) =>
    api.get<Property[]>('/properties/search', { params }),
  mine: () => api.get<Property[]>('/properties/user/my-properties'),
  create: (body: CreatePropertyRequest) => api.post<Property>('/properties', body),
  update: (id: string, body: CreatePropertyRequest) => api.put<Property>(`/properties/${id}`, body),
  remove: (id: string) => api.delete<Record<string, never>>(`/properties/${id}`),
  uploadPhotos: (id: string, files: File[]) => {
    const fd = new FormData();
    files.forEach((f) => fd.append('files', f));
    return api.post<Property>(`/properties/${id}/photos`, fd);
  },
  walkthroughs: (id: string) => api.get<Walkthrough[]>(`/properties/${id}/walkthroughs`),
};

export const reviewsApi = {
  forProperty: (propertyId: string, page = 1, pageSize = 20) =>
    api.get<PagedResult<Review>>(`/reviews/property/${propertyId}`, { params: { page, pageSize } }),
  create: (body: { bookingId: string; propertyId: string; rating: number; comment?: string }) =>
    api.post<Review>('/reviews', body),
  mine: () => api.get<Review[]>('/reviews/mine'),
  remove: (id: string) => api.delete<Record<string, never>>(`/reviews/${id}`),
};

export const trustApi = {
  forProperty: (propertyId: string) => api.get<TrustScore>(`/trustscore/property/${propertyId}`),
  forUser: (userId: string) => api.get<TrustScore>(`/trustscore/user/${userId}`),
  stayFeedback: (body: {
    bookingId: string;
    accuracyRating: number;
    cleanlinessRating: number;
    safetyRating: number;
    comment?: string;
  }) => api.post<TrustScore>('/trustscore/stay-feedback', body),
};

export const agentsApi = {
  list: () => api.get<Agent[]>('/agents'),
  get: (id: string) => api.get<Agent>(`/agents/${id}`),
  requestViewing: (id: string, body: { propertyId: string; scheduledAt: string; notes?: string }) =>
    api.post<ViewingRequest>(`/agents/${id}/viewing-requests`, body),
  updateViewing: (id: string, status: string) =>
    api.patch<ViewingRequest>(`/agents/viewing-requests/${id}/status`, { status }),
};

export const caretakersApi = {
  list: () => api.get<Caretaker[]>('/caretakers'),
  get: (id: string) => api.get<Caretaker>(`/caretakers/${id}`),
  createServiceRequest: (body: {
    propertyId?: string;
    caretakerId?: string;
    serviceType: string;
    description: string;
    scheduledFor?: string;
  }) => api.post<ServiceRequest>('/caretakers/service-requests', body),
  myServiceRequests: () => api.get<ServiceRequest[]>('/caretakers/service-requests/mine'),
  acceptServiceRequest: (id: string) => api.patch<ServiceRequest>(`/caretakers/service-requests/${id}/accept`),
  updateServiceRequest: (id: string, status: string) =>
    api.patch<ServiceRequest>(`/caretakers/service-requests/${id}/status`, { status }),
  reviewServiceRequest: (id: string, body: { rating: number; comment?: string }) =>
    api.post<ServiceRequest>(`/caretakers/service-requests/${id}/review`, body),
};

export const bookingsApi = {
  get: (id: string) => api.get<Booking>(`/bookings/${id}`),
  create: (body: CreateBookingRequest) => api.post<Booking>('/bookings', body),
  mine: () => api.get<Booking[]>('/bookings/user/my-bookings'),
  cancellationPreview: (id: string) =>
    api.get<{ refundPercentage: number; refundAmount: number }>(`/bookings/${id}/cancellation-preview`),
  cancel: (id: string) => api.post<Booking>(`/bookings/${id}/cancel`),
};

export const escrowApi = {
  initiate: (bookingId: string) => api.post<Escrow>('/escrow/initiate', { bookingId }),
  get: (id: string) => api.get<Escrow>(`/escrow/${id}`),
  release: (id: string) => api.post<Escrow>(`/escrow/${id}/release`),
  dispute: (id: string, reason: string) => api.post<Escrow>(`/escrow/${id}/dispute`, { reason }),
  resolveDispute: (id: string, approved: boolean) => api.patch<Escrow>(`/escrow/${id}/resolve-dispute`, { approved }),
  refund: (id: string, reason: string) => api.post<Escrow>(`/escrow/${id}/refund`, { reason }),
};

export const agreementsApi = {
  create: (bookingId: string) => api.post<Agreement>('/agreements', { bookingId }),
  mine: () => api.get<Agreement[]>('/agreements/mine'),
  get: (id: string) => api.get<Agreement>(`/agreements/${id}`),
  sign: (id: string) => api.post<Agreement>(`/agreements/${id}/sign`),
  downloadUrl: (id: string) => `/api/agreements/${id}/download`,
};

export const chatApi = {
  conversations: () => api.get<Conversation[]>('/chat/conversations/mine'),
  start: (otherUserId: string, propertyId?: string) =>
    api.post<Conversation>('/chat/conversations', { otherUserId, propertyId }),
  get: (id: string) => api.get<Conversation>(`/chat/conversations/${id}`),
  messages: (id: string, page = 1, pageSize = 30) =>
    api.get<PagedResult<Message>>(`/chat/conversations/${id}/messages`, { params: { page, pageSize } }),
  send: (id: string, body: string) => api.post<Message>(`/chat/conversations/${id}/messages`, { body }),
  markRead: (id: string) => api.patch<Record<string, never>>(`/chat/conversations/${id}/mark-read`),
};

export const maintenanceApi = {
  report: (body: { propertyId: string; category: string; description: string; priority: string; photoPaths?: string[] }) =>
    api.post<MaintenanceRequest>('/maintenance', body),
  mine: () => api.get<MaintenanceRequest[]>('/maintenance/mine'),
  forProperty: (propertyId: string) => api.get<MaintenanceRequest[]>(`/maintenance/property/${propertyId}`),
  updateStatus: (id: string, status: string) => api.patch<MaintenanceRequest>(`/maintenance/${id}/status`, { status }),
  convertToServiceRequest: (id: string, caretakerId?: string) =>
    api.post<ServiceRequest>(`/maintenance/${id}/convert-to-service-request`, { caretakerId }),
};

export const notificationsApi = {
  mine: (page = 1, pageSize = 20) =>
    api.get<PagedResult<Notification>>('/notifications/mine', { params: { page, pageSize } }),
  unreadCount: () => api.get<{ count: number }>('/notifications/unread-count'),
  markRead: (id: string) => api.patch<Record<string, never>>(`/notifications/${id}/read`),
  markAllRead: () => api.patch<Record<string, never>>('/notifications/mark-all-read'),
  remove: (id: string) => api.delete<Record<string, never>>(`/notifications/${id}`),
};

export const prefsApi = {
  get: () => api.get<CommunicationPreference>('/communication-preferences/mine'),
  update: (smsEnabled: boolean, emailEnabled: boolean) =>
    api.put<CommunicationPreference>('/communication-preferences/mine', { smsEnabled, emailEnabled }),
};

export const receiptsApi = {
  mine: (page = 1, pageSize = 20) =>
    api.get<PagedResult<Receipt>>('/receipts/mine', { params: { page, pageSize } }),
  forBooking: (bookingId: string) => api.get<Receipt>(`/receipts/booking/${bookingId}`),
  downloadUrl: (id: string) => `/api/receipts/${id}/download`,
};

export const wishlistApi = {
  mine: () => api.get<Property[]>('/wishlist/mine'),
  add: (propertyId: string) => api.post<Record<string, never>>(`/wishlist/${propertyId}`),
  remove: (propertyId: string) => api.delete<Record<string, never>>(`/wishlist/${propertyId}`),
};

export const profileApi = {
  me: () => api.get<Record<string, unknown>>('/profile/me'),
  update: (body: { fullName?: string }) => api.put<Record<string, unknown>>('/profile/me', body),
  // Returns the stored, servable path; this same path is reused as the verification selfie.
  uploadPhoto: (file: File) => {
    const fd = new FormData();
    fd.append('photo', file);
    return api.post<{ photoPath: string }>('/profile/photo', fd);
  },
  idCardUrl: () => '/api/profile/id-card',
};

export const verificationApi = {
  start: (body: StartVerificationRequest) => api.post<VerificationStatusResponse>('/verification/start', body),
  status: () => api.get<VerificationStatusResponse>('/verification/status'),
};

export const safetyApi = {
  getContact: () => api.get<TrustedContact>('/safety/contact'),
  setContact: (body: TrustedContact) => api.put<TrustedContact>('/safety/contact', body),
  checkIn: (body: {
    bookingId: string;
    contactPhone?: string;
    contactEmail?: string;
    shareLocation: boolean;
    latitude?: number;
    longitude?: number;
  }) => api.post<{ checkInId: string; contactNotified: boolean; locationShared: boolean }>('/safety/checkin', body),
  alert: (bookingId: string) => api.post<Record<string, never>>('/safety/alert', { bookingId }),
};

export const searchApi = {
  global: (q: string, type?: string) =>
    api.get<GlobalSearchResult[]>('/search', { params: { q, type } }),
};

export const dashboardApi = {
  tenant: () => api.get<Record<string, unknown>>('/personaldashboard/tenant'),
  landlord: () => api.get<Record<string, unknown>>('/personaldashboard/landlord'),
  agent: () => api.get<Record<string, unknown>>('/personaldashboard/agent'),
  caretaker: () => api.get<Record<string, unknown>>('/personaldashboard/caretaker'),
  landlordStats: () => api.get<Record<string, unknown>>('/landlord/stats'),
  landlordEarnings: () => api.get<Record<string, unknown>>('/landlord/earnings'),
  adminStats: () => api.get<AdminStats>('/admin/stats'),
};
