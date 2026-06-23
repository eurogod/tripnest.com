import { NavLink, Outlet, useLocation, Navigate } from 'react-router-dom';
import type { ReactNode } from 'react';
import { TopBar } from './TopBar';
import { Footer } from './Footer';
import { Spinner } from './ui';
import { useAuth } from '@/auth/AuthContext';
import { UserRole } from '@/lib/enums';
import { Home, Calendar, Heart, Chat, Bell, Cash, Doc, Wrench, Users, Shield, Settings, Camera, MapPin } from './icons';

export function PublicLayout() {
  return (
    <div className="flex min-h-full flex-col">
      <TopBar />
      <main className="flex-1">
        <Outlet />
      </main>
      <Footer />
    </div>
  );
}

/** Plain shell for full-bleed pages (search split-view) — no footer. */
export function BareLayout() {
  return (
    <div className="flex h-full flex-col">
      <TopBar />
      <main className="min-h-0 flex-1">
        <Outlet />
      </main>
    </div>
  );
}

function FullScreenLoader() {
  return (
    <div className="grid h-full place-items-center text-brand-600">
      <Spinner className="h-8 w-8" />
    </div>
  );
}

export function ProtectedRoute({ children }: { children: ReactNode }) {
  const { user, loading } = useAuth();
  const location = useLocation();
  if (loading) return <FullScreenLoader />;
  if (!user) return <Navigate to="/login" state={{ from: location.pathname }} replace />;
  return <>{children}</>;
}

export function RoleRoute({ roles, children }: { roles: UserRole[]; children: ReactNode }) {
  const { user, loading } = useAuth();
  if (loading) return <FullScreenLoader />;
  if (!user) return <Navigate to="/login" replace />;
  if (!roles.includes(user.role)) return <Navigate to="/dashboard" replace />;
  return <>{children}</>;
}

interface NavItem {
  to: string;
  label: string;
  icon: ReactNode;
}

const ic = 'h-5 w-5';
const navByRole: Record<number, NavItem[]> = {
  [UserRole.Tenant]: [
    { to: '/dashboard', label: 'Overview', icon: <Home className={ic} /> },
    { to: '/dashboard/trips', label: 'My trips', icon: <Calendar className={ic} /> },
    { to: '/wishlist', label: 'Saved', icon: <Heart className={ic} /> },
    { to: '/dashboard/agreements', label: 'Agreements', icon: <Doc className={ic} /> },
    { to: '/dashboard/payments', label: 'Receipts', icon: <Cash className={ic} /> },
    { to: '/dashboard/maintenance', label: 'Maintenance', icon: <Wrench className={ic} /> },
    { to: '/messages', label: 'Messages', icon: <Chat className={ic} /> },
    { to: '/verification', label: 'Verification', icon: <Shield className={ic} /> },
    { to: '/settings', label: 'Settings', icon: <Settings className={ic} /> },
  ],
  [UserRole.Landlord]: [
    { to: '/host', label: 'Overview', icon: <Home className={ic} /> },
    { to: '/host/listings', label: 'Listings', icon: <MapPin className={ic} /> },
    { to: '/host/listings/new', label: 'Add property', icon: <Camera className={ic} /> },
    { to: '/host/reservations', label: 'Reservations', icon: <Calendar className={ic} /> },
    { to: '/host/earnings', label: 'Earnings & escrow', icon: <Cash className={ic} /> },
    { to: '/messages', label: 'Messages', icon: <Chat className={ic} /> },
    { to: '/verification', label: 'Verification', icon: <Shield className={ic} /> },
    { to: '/settings', label: 'Settings', icon: <Settings className={ic} /> },
  ],
  [UserRole.Agent]: [
    { to: '/agent', label: 'Overview', icon: <Home className={ic} /> },
    { to: '/agent/viewings', label: 'Viewing requests', icon: <Calendar className={ic} /> },
    { to: '/messages', label: 'Messages', icon: <Chat className={ic} /> },
    { to: '/verification', label: 'Verification', icon: <Shield className={ic} /> },
    { to: '/settings', label: 'Settings', icon: <Settings className={ic} /> },
  ],
  [UserRole.Caretaker]: [
    { to: '/caretaker', label: 'Overview', icon: <Home className={ic} /> },
    { to: '/caretaker/requests', label: 'Service requests', icon: <Wrench className={ic} /> },
    { to: '/messages', label: 'Messages', icon: <Chat className={ic} /> },
    { to: '/verification', label: 'Verification', icon: <Shield className={ic} /> },
    { to: '/settings', label: 'Settings', icon: <Settings className={ic} /> },
  ],
  [UserRole.Admin]: [
    { to: '/admin', label: 'Overview', icon: <Home className={ic} /> },
    { to: '/admin/walkthroughs', label: 'Walkthroughs', icon: <Camera className={ic} /> },
    { to: '/admin/disputes', label: 'Disputes', icon: <Cash className={ic} /> },
    { to: '/admin/users', label: 'Users', icon: <Users className={ic} /> },
    { to: '/settings', label: 'Settings', icon: <Settings className={ic} /> },
  ],
};

export function DashboardLayout() {
  const { user } = useAuth();
  const items = navByRole[user?.role ?? UserRole.Tenant] ?? navByRole[UserRole.Tenant];

  return (
    <div className="flex min-h-full flex-col">
      <TopBar />
      <div className="container-tn flex flex-1 gap-6 py-6">
        <aside className="sticky top-20 hidden h-[calc(100vh-6rem)] w-60 shrink-0 lg:block">
          <nav className="flex flex-col gap-1">
            {items.map((it) => (
              <NavLink
                key={it.to}
                to={it.to}
                end={it.to.split('/').length <= 2}
                className={({ isActive }) =>
                  `flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-semibold transition ${
                    isActive ? 'bg-brand-50 text-brand-700' : 'text-ink hover:bg-black/5'
                  }`
                }
              >
                {it.icon}
                {it.label}
              </NavLink>
            ))}
          </nav>
        </aside>
        <div className="min-w-0 flex-1">
          <Outlet />
        </div>
      </div>
      {/* Mobile bottom nav */}
      <nav className="sticky bottom-0 z-40 flex items-center justify-around border-t border-line bg-white/95 py-2 backdrop-blur lg:hidden">
        {items.slice(0, 5).map((it) => (
          <NavLink
            key={it.to}
            to={it.to}
            end={it.to.split('/').length <= 2}
            className={({ isActive }) =>
              `flex flex-col items-center gap-0.5 px-2 text-[10px] font-semibold ${isActive ? 'text-brand-700' : 'text-muted'}`
            }
          >
            {it.icon}
            {it.label}
          </NavLink>
        ))}
      </nav>
    </div>
  );
}
