import { apiGet } from './client';

// Wire shapes of the role home dashboards. Sources:
// PersonalDashboardController (agent/caretaker) and DashboardController (admin stats).

export interface AgentDashboardDto {
  totalWalkthroughs: number;
  propertiesWithWalkthroughs: number;
  propertiesWithoutWalkthroughs: number;
  recentWalkthroughsCount: number;
  recentActivity: {
    lastWalkthroughDate: string | null;
    totalVideoHours: number;
  };
}

export interface CaretakerDashboardDto {
  totalServiceRequests: number;
  activeServiceRequests: number;
  completedServiceRequests: number;
  pendingRequests: number;
  averageRating: number;
  totalReviews: number;
  earningsThisMonth: number;
  recentActivity: { message: string };
}

export interface AdminStatsDto {
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
}

export const getAgentDashboard = () =>
  apiGet<AgentDashboardDto>('/api/personaldashboard/agent');

export const getCaretakerDashboard = () =>
  apiGet<CaretakerDashboardDto>('/api/personaldashboard/caretaker');

export const getAdminStats = () =>
  apiGet<AdminStatsDto>('/api/admin/stats');
