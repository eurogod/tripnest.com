import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import MarketplaceLayout from './layouts/MarketplaceLayout';
import DashboardLayout from './layouts/DashboardLayout';
import LandlordLayout from './layouts/LandlordLayout';
import RequireAuth from './components/RequireAuth';
import { useSession } from './store/authStore';
import WelcomePage from './pages/WelcomePage';
import GetVerifiedPage from './pages/GetVerifiedPage';
import LandlordHome from './pages/landlord/LandlordHome';
import EarningsPage from './pages/landlord/EarningsPage';
import LandlordListingsPage from './pages/landlord/ListingsPage';
import ListingGalleryPage from './pages/landlord/ListingGalleryPage';
import InquiriesPage from './pages/landlord/InquiriesPage';
import LandlordMessagesPage from './pages/landlord/MessagesPage';
import LandlordBookingsPage from './pages/landlord/BookingsPage';
import TenantsPage from './pages/landlord/TenantsPage';
import ReviewsPage from './pages/landlord/ReviewsPage';
import LandlordSettingsPage from './pages/landlord/SettingsPage';
import LandlordHelpPage from './pages/landlord/HelpPage';
import PagePlaceholder from './components/PagePlaceholder';
import type { ReactNode } from 'react';
import { TENANT_NAV_ITEMS, TENANT_ROLE_PATHS } from './components/tenant/navConfig';
import { lazy, Suspense } from 'react';
import HomePage from './pages/tenant/HomePage';
import SearchPage from './pages/tenant/SearchPage';
import ExplorePage from './pages/tenant/ExplorePage';

// Map page pulls in Leaflet — load it on demand to keep the main bundle lean.
const NearbyPage = lazy(() => import('./pages/tenant/NearbyPage'));
import SavedPage from './pages/tenant/SavedPage';
import PropertyDetailPage from './pages/tenant/PropertyDetailPage';
import CheckoutPage from './pages/tenant/CheckoutPage';
import PaymentCallbackPage from './pages/tenant/PaymentCallbackPage';
import XCallbackPage from './pages/XCallbackPage';
import RoommatesPage from './pages/tenant/RoommatesPage';
import BookingsPage from './pages/tenant/BookingsPage';
import AgreementsPage from './pages/tenant/AgreementsPage';
import PaymentsPage from './pages/tenant/PaymentsPage';
import MessagesPage from './pages/tenant/MessagesPage';
import NotificationsPage from './pages/tenant/NotificationsPage';
import ServiceDirectory from './pages/tenant/ServiceDirectory';
import ProviderDetailPage from './pages/tenant/ProviderDetailPage';
import MaintenancePage from './pages/tenant/MaintenancePage';
import TenantDashboardPage from './pages/tenant/DashboardPage';
import ProfilePage from './pages/tenant/ProfilePage';
import SettingsPage from './pages/tenant/SettingsPage';
import HelpPage from './pages/tenant/HelpPage';
import RoleWorkspaceLayout from './layouts/RoleWorkspaceLayout';
import WorkspaceMessagesPage from './components/workspace/WorkspaceMessagesPage';
import { AGENT_NAV, CARETAKER_NAV, ADMIN_NAV } from './components/workspace/roleNavs';
import AgentHomePage from './pages/agent/AgentHomePage';
import WalkthroughReviewPage from './pages/agent/WalkthroughReviewPage';
import ViewingRequestsPage from './pages/agent/ViewingRequestsPage';
import AgentProfilePage from './pages/agent/AgentProfilePage';
import CaretakerHomePage from './pages/caretaker/CaretakerHomePage';
import ServiceRequestsPage from './pages/caretaker/ServiceRequestsPage';
import AdminHomePage from './pages/admin/AdminHomePage';
import DisputesPage from './pages/admin/DisputesPage';
import SupportTicketsPage from './pages/admin/SupportTicketsPage';
import AuditLogsPage from './pages/admin/AuditLogsPage';
import { homeForRole } from './lib/roleHome';
import OverviewPage from './pages/OverviewPage';
import ReservationPage from './pages/ReservationPage';
import CalendarPage from './pages/CalendarPage';
import PricingPage from './pages/PricingPage';
import StatementsPage from './pages/StatementsPage';
import ListingsPage from './pages/ListingsPage';
import TasksPage from './pages/TasksPage';
import MyTripsPage from './pages/MyTripsPage';
import UsersPage from './pages/UsersPage';
import OwnerExchangePage from './pages/OwnerExchangePage';
import ResourcesPage from './pages/ResourcesPage';

/** Visitors are onboarded through Explore first; signed-in users get their home. */
function Landing() {
  const session = useSession();
  if (!session) return <Navigate to="/explore" replace />;
  const home = homeForRole(session.role);
  return home === '/' ? <HomePage /> : <Navigate to={home} replace />;
}

const TENANT_PAGES: Record<string, ReactNode> = {
  '/': <Landing />,
  '/overview': <TenantDashboardPage />,
  '/roommates': <RoommatesPage />,
  '/search': <SearchPage />,
  '/nearby': (
    <Suspense fallback={<p className="text-muted">Loading map…</p>}>
      <NearbyPage />
    </Suspense>
  ),
  '/saved': <SavedPage />,
  '/bookings': <BookingsPage />,
  '/agreements': <AgreementsPage />,
  '/payments': <PaymentsPage />,
  '/messages': <MessagesPage />,
  '/notifications': <NotificationsPage />,
  '/caretakers': (
    <ServiceDirectory category="Caretakers" title="Caretakers" subtitle="Verified, on-site caretakers you can rely on." />
  ),
  '/house-help': (
    <ServiceDirectory category="House Help" title="House Help" subtitle="Trusted cleaners and housekeepers near you." />
  ),
  '/maintenance': <MaintenancePage />,
  '/agents': (
    <ServiceDirectory category="Agents" title="Agents" subtitle="Verified agents to help you find the right home." />
  ),
  '/profile': <ProfilePage />,
  '/settings': <SettingsPage />,
  '/help': <HelpPage />,
};

// Marketplace pages that need a signed-in user. Everything else (home,
// search, nearby, property pages, service directories) is open to guests.
const AUTH_ONLY_TENANT_PATHS = new Set([
  '/overview', '/saved', '/bookings', '/agreements', '/payments', '/messages',
  '/notifications', '/maintenance', '/profile', '/settings', '/roommates',
]);

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* Onboarding: Explore introduces TripNest, then hands off to auth */}
        <Route path="/explore" element={<ExplorePage />} />
        <Route path="/welcome" element={<WelcomePage />} />
        <Route path="/get-verified" element={<RequireAuth><GetVerifiedPage /></RequireAuth>} />
        {/* X OAuth redirect target — public: the user is not signed in yet when X sends them back. */}
        <Route path="/auth/x/callback" element={<XCallbackPage />} />

        {/* Tenant marketplace — browsing is public; personal pages need a login */}
        <Route path="/" element={<MarketplaceLayout />}>
          {TENANT_NAV_ITEMS.map((item) => {
            let element: ReactNode = TENANT_PAGES[item.path] ?? (
              <PagePlaceholder title={item.label} />
            );
            if (AUTH_ONLY_TENANT_PATHS.has(item.path)) {
              element = (
                <RequireAuth role={TENANT_ROLE_PATHS.has(item.path) ? 'tenant' : undefined}>
                  {element}
                </RequireAuth>
              );
            }
            return item.path === '/' ? (
              <Route key={item.path} index element={element} />
            ) : (
              <Route key={item.path} path={item.path.slice(1)} element={element} />
            );
          })}
          <Route path="messages/:conversationId" element={<RequireAuth><MessagesPage /></RequireAuth>} />
          <Route path="property/:id" element={<PropertyDetailPage />} />
          <Route path="providers/:id" element={<ProviderDetailPage />} />
          <Route path="checkout/:id" element={<RequireAuth><CheckoutPage /></RequireAuth>} />
          <Route path="payment/callback" element={<RequireAuth><PaymentCallbackPage /></RequireAuth>} />
        </Route>

        {/* Landlord marketplace — only landlord accounts. Like all Core-gated roles
            (Landlord/Agent/Caretaker), hosting requires a verified identity. */}
        <Route path="/landlord" element={<RequireAuth role="landlord" verified><LandlordLayout /></RequireAuth>}>
          <Route index element={<LandlordHome />} />
          <Route path="reservations" element={<ReservationPage />} />
          <Route path="calendar" element={<CalendarPage />} />
          <Route path="pricing" element={<PricingPage />} />
          <Route path="statements" element={<StatementsPage />} />
          <Route path="listings" element={<LandlordListingsPage />} />
          <Route path="listings/:id" element={<ListingGalleryPage />} />
          <Route path="messages" element={<LandlordMessagesPage />} />
          <Route path="messages/:conversationId" element={<LandlordMessagesPage />} />
          <Route path="inquiries" element={<InquiriesPage />} />
          <Route path="bookings" element={<LandlordBookingsPage />} />
          <Route path="earnings" element={<EarningsPage />} />
          <Route path="agreements" element={<AgreementsPage />} />
          <Route path="tenants" element={<TenantsPage />} />
          <Route path="reviews" element={<ReviewsPage />} />
          <Route path="settings" element={<LandlordSettingsPage />} />
          <Route path="help" element={<LandlordHelpPage />} />
        </Route>

        {/* Role workspaces — where homeForRole sends agents, caretakers and admins */}
        <Route
          path="/agent"
          element={
            <RequireAuth role="agent" verified>
              <RoleWorkspaceLayout nav={AGENT_NAV} roleLabel="Agent" accountPath="/agent/profile" />
            </RequireAuth>
          }
        >
          <Route index element={<AgentHomePage />} />
          <Route path="walkthroughs" element={<WalkthroughReviewPage />} />
          <Route path="viewings" element={<ViewingRequestsPage />} />
          <Route path="messages" element={<WorkspaceMessagesPage basePath="/agent/messages" subtitle="Chat with tenants about viewings and walkthroughs." />} />
          <Route path="messages/:conversationId" element={<WorkspaceMessagesPage basePath="/agent/messages" subtitle="Chat with tenants about viewings and walkthroughs." />} />
          <Route path="profile" element={<AgentProfilePage />} />
        </Route>
        <Route
          path="/caretaker"
          element={
            <RequireAuth role="caretaker" verified>
              <RoleWorkspaceLayout nav={CARETAKER_NAV} roleLabel="Caretaker" accountPath="/caretaker" />
            </RequireAuth>
          }
        >
          <Route index element={<CaretakerHomePage />} />
          <Route path="requests" element={<ServiceRequestsPage />} />
          <Route path="messages" element={<WorkspaceMessagesPage basePath="/caretaker/messages" subtitle="Chat with tenants and landlords about your assignments." />} />
          <Route path="messages/:conversationId" element={<WorkspaceMessagesPage basePath="/caretaker/messages" subtitle="Chat with tenants and landlords about your assignments." />} />
        </Route>
        {/* Admins are exempt from identity verification (Core's RequireVerified
            gates Landlord/Agent/Caretaker only). */}
        <Route
          path="/admin"
          element={
            <RequireAuth role="admin">
              <RoleWorkspaceLayout nav={ADMIN_NAV} roleLabel="Admin" accountPath="/admin" />
            </RequireAuth>
          }
        >
          <Route index element={<AdminHomePage />} />
          <Route path="disputes" element={<DisputesPage />} />
          <Route path="tickets" element={<SupportTicketsPage />} />
          <Route path="walkthroughs" element={<WalkthroughReviewPage />} />
          <Route path="messages" element={<WorkspaceMessagesPage basePath="/admin/messages" subtitle="Chat with users about escalations and disputes." />} />
          <Route path="messages/:conversationId" element={<WorkspaceMessagesPage basePath="/admin/messages" subtitle="Chat with users about escalations and disputes." />} />
          <Route path="audit" element={<AuditLogsPage />} />
        </Route>

        {/* Host management dashboard — landlord-only */}
        <Route path="/dashboard" element={<RequireAuth role="landlord" verified><DashboardLayout /></RequireAuth>}>
          <Route index element={<Navigate to="overview" replace />} />
          <Route path="overview" element={<OverviewPage />} />
          <Route path="reservations" element={<ReservationPage />} />
          <Route path="calendar" element={<CalendarPage />} />
          <Route path="pricing" element={<PricingPage />} />
          <Route path="statements" element={<StatementsPage />} />
          <Route path="listings" element={<ListingsPage />} />
          <Route path="tasks" element={<TasksPage />} />
          <Route path="my-trips" element={<MyTripsPage />} />
          <Route path="users" element={<UsersPage />} />
          <Route path="owner-exchange" element={<OwnerExchangePage />} />
          <Route path="resources" element={<ResourcesPage />} />
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
