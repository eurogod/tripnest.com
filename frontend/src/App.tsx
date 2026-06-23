import { Routes, Route, Navigate } from 'react-router-dom';
import { BareLayout, DashboardLayout, ProtectedRoute, PublicLayout, RoleRoute } from './components/layouts';
import { UserRole } from './lib/enums';

import LandingPage from './pages/LandingPage';
import SearchPage from './pages/SearchPage';
import PropertyDetailPage from './pages/PropertyDetailPage';
import ServicesPage from './pages/ServicesPage';
import AboutPage from './pages/AboutPage';

import LoginPage from './pages/auth/LoginPage';
import SignupPage from './pages/auth/SignupPage';
import ForgotPasswordPage from './pages/auth/ForgotPasswordPage';
import ResetPasswordPage from './pages/auth/ResetPasswordPage';

import VerificationPage from './pages/VerificationPage';
import IdCardPage from './pages/IdCardPage';
import WishlistPage from './pages/WishlistPage';
import NotificationsPage from './pages/NotificationsPage';
import MessagesPage from './pages/MessagesPage';
import SettingsPage from './pages/SettingsPage';

import TenantOverview from './pages/tenant/TenantOverview';
import TripsPage from './pages/tenant/TripsPage';
import AgreementsPage from './pages/tenant/AgreementsPage';
import ReceiptsPage from './pages/tenant/ReceiptsPage';
import MaintenancePage from './pages/tenant/MaintenancePage';

import HostOverview from './pages/host/HostOverview';
import HostListings from './pages/host/HostListings';
import NewListing from './pages/host/NewListing';
import HostReservations from './pages/host/HostReservations';
import HostEarnings from './pages/host/HostEarnings';

import AgentOverview from './pages/agent/AgentOverview';
import AgentViewings from './pages/agent/AgentViewings';
import CaretakerOverview from './pages/caretaker/CaretakerOverview';
import CaretakerRequests from './pages/caretaker/CaretakerRequests';

import AdminOverview from './pages/admin/AdminOverview';
import AdminWalkthroughs from './pages/admin/AdminWalkthroughs';
import AdminDisputes from './pages/admin/AdminDisputes';
import AdminUsers from './pages/admin/AdminUsers';

import NotFound from './pages/NotFound';

export default function App() {
  return (
    <Routes>
      {/* Full-bleed search (own layout, no footer) */}
      <Route element={<BareLayout />}>
        <Route path="/search" element={<SearchPage />} />
      </Route>

      {/* Public marketing + content */}
      <Route element={<PublicLayout />}>
        <Route path="/" element={<LandingPage />} />
        <Route path="/property/:id" element={<PropertyDetailPage />} />
        <Route path="/services" element={<ServicesPage />} />
        <Route path="/about" element={<AboutPage />} />

        {/* Auth-required, single-column pages */}
        <Route path="/verification" element={<ProtectedRoute><VerificationPage /></ProtectedRoute>} />
        <Route path="/id-card" element={<ProtectedRoute><IdCardPage /></ProtectedRoute>} />
        <Route path="/wishlist" element={<ProtectedRoute><WishlistPage /></ProtectedRoute>} />
        <Route path="/notifications" element={<ProtectedRoute><NotificationsPage /></ProtectedRoute>} />
        <Route path="/messages" element={<ProtectedRoute><MessagesPage /></ProtectedRoute>} />
        <Route path="/settings" element={<ProtectedRoute><SettingsPage /></ProtectedRoute>} />
      </Route>

      {/* Auth pages (no chrome) */}
      <Route path="/login" element={<LoginPage />} />
      <Route path="/signup" element={<SignupPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/reset-password" element={<ResetPasswordPage />} />

      {/* Tenant dashboard */}
      <Route element={<ProtectedRoute><DashboardLayout /></ProtectedRoute>}>
        <Route path="/dashboard" element={<TenantOverview />} />
        <Route path="/dashboard/trips" element={<TripsPage />} />
        <Route path="/dashboard/agreements" element={<AgreementsPage />} />
        <Route path="/dashboard/payments" element={<ReceiptsPage />} />
        <Route path="/dashboard/maintenance" element={<MaintenancePage />} />
      </Route>

      {/* Landlord / host */}
      <Route element={<RoleRoute roles={[UserRole.Landlord]}><DashboardLayout /></RoleRoute>}>
        <Route path="/host" element={<HostOverview />} />
        <Route path="/host/listings" element={<HostListings />} />
        <Route path="/host/listings/new" element={<NewListing />} />
        <Route path="/host/reservations" element={<HostReservations />} />
        <Route path="/host/earnings" element={<HostEarnings />} />
      </Route>

      {/* Agent */}
      <Route element={<RoleRoute roles={[UserRole.Agent]}><DashboardLayout /></RoleRoute>}>
        <Route path="/agent" element={<AgentOverview />} />
        <Route path="/agent/viewings" element={<AgentViewings />} />
      </Route>

      {/* Caretaker */}
      <Route element={<RoleRoute roles={[UserRole.Caretaker]}><DashboardLayout /></RoleRoute>}>
        <Route path="/caretaker" element={<CaretakerOverview />} />
        <Route path="/caretaker/requests" element={<CaretakerRequests />} />
      </Route>

      {/* Admin */}
      <Route element={<RoleRoute roles={[UserRole.Admin]}><DashboardLayout /></RoleRoute>}>
        <Route path="/admin" element={<AdminOverview />} />
        <Route path="/admin/walkthroughs" element={<AdminWalkthroughs />} />
        <Route path="/admin/disputes" element={<AdminDisputes />} />
        <Route path="/admin/users" element={<AdminUsers />} />
      </Route>

      <Route path="/404" element={<NotFound />} />
      <Route path="*" element={<Navigate to="/404" replace />} />
    </Routes>
  );
}
