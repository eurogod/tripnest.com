import { NavLink } from 'react-router-dom';
import type { NavPage } from '../types';

interface NavItem {
  id: NavPage;
  label: string;
  icon: React.ReactNode;
}

const IconChart = () => (
  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="22 12 18 12 15 21 9 3 6 12 2 12" />
  </svg>
);
const IconClipboardList = () => (
  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <rect x="8" y="2" width="8" height="4" rx="1" /><path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2" /><line x1="9" y1="11" x2="15" y2="11" /><line x1="9" y1="15" x2="15" y2="15" />
  </svg>
);
const IconCalendar = () => (
  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <rect x="3" y="4" width="18" height="18" rx="2" /><line x1="16" y1="2" x2="16" y2="6" /><line x1="8" y1="2" x2="8" y2="6" /><line x1="3" y1="10" x2="21" y2="10" />
  </svg>
);
const IconCoin = () => (
  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <circle cx="12" cy="12" r="10" /><path d="M12 8v8M9 10h4.5a1.5 1.5 0 0 1 0 3H9m0 0h4.5" />
  </svg>
);
const IconFile = () => (
  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" /><polyline points="14 2 14 8 20 8" />
  </svg>
);
const IconHome = () => (
  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" /><polyline points="9 22 9 12 15 12 15 22" />
  </svg>
);
const IconCheck = () => (
  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="20 6 9 17 4 12" />
  </svg>
);
const IconMapPin = () => (
  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" /><circle cx="12" cy="10" r="3" />
  </svg>
);
const IconUsers = () => (
  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" /><circle cx="9" cy="7" r="4" /><path d="M23 21v-2a4 4 0 0 0-3-3.87" /><path d="M16 3.13a4 4 0 0 1 0 7.75" />
  </svg>
);
const IconSend = () => (
  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <line x1="22" y1="2" x2="11" y2="13" /><polygon points="22 2 15 22 11 13 2 9 22 2" />
  </svg>
);
const IconBook = () => (
  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20" /><path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z" />
  </svg>
);

const Globe = () => (
  <div className="flex h-9 w-9 items-center justify-center rounded-full bg-gray-900 text-white">
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
      <circle cx="12" cy="12" r="10" />
      <line x1="2" y1="12" x2="22" y2="12" />
      <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z" />
    </svg>
  </div>
);

const PRIMARY_NAV: NavItem[] = [
  { id: 'overview', label: 'Overview', icon: <IconChart /> },
  { id: 'reservations', label: 'Reservations', icon: <IconClipboardList /> },
  { id: 'calendar', label: 'Calendar', icon: <IconCalendar /> },
  { id: 'pricing', label: 'Pricing', icon: <IconCoin /> },
  { id: 'statements', label: 'Statements', icon: <IconFile /> },
];

const SECONDARY_NAV: NavItem[] = [
  { id: 'listings', label: 'Listings', icon: <IconHome /> },
  { id: 'tasks', label: 'Tasks', icon: <IconCheck /> },
  { id: 'my-trips', label: 'My Trips', icon: <IconMapPin /> },
];

const BOTTOM_NAV: NavItem[] = [
  { id: 'users', label: 'Users', icon: <IconUsers /> },
  { id: 'owner-exchange', label: 'Owner Exchange', icon: <IconSend /> },
  { id: 'resources', label: 'Resources', icon: <IconBook /> },
];

const baseItem =
  'flex w-full items-center gap-3 rounded-[10px] px-3.5 py-3 text-[15px] font-medium no-underline transition-colors';
const inactiveItem = 'text-gray-600 hover:bg-slate-100 hover:text-gray-900';
const activeItem = 'bg-white border border-gray-400 font-semibold text-gray-900 shadow-[0_1px_2px_rgba(0,0,0,0.06),0_1px_8px_rgba(0,0,0,0.04)]';

interface SidebarProps {
  open: boolean;
  onClose: () => void;
}

function NavList({ items, onNavigate }: { items: NavItem[]; onNavigate: () => void }) {
  return (
    <nav className="flex flex-col gap-1">
      {items.map((item) => (
        <NavLink
          key={item.id}
          to={`/dashboard/${item.id}`}
          onClick={onNavigate}
          className={({ isActive }) =>
            `${baseItem} ${isActive ? activeItem : inactiveItem}`
          }
        >
          <span className="flex shrink-0 items-center justify-center">{item.icon}</span>
          {item.label}
        </NavLink>
      ))}
    </nav>
  );
}

export default function Sidebar({ open, onClose }: SidebarProps) {
  return (
    <>
      {open && (
        <div
          className="fixed inset-0 z-40 bg-black/30 lg:hidden"
          onClick={onClose}
          aria-hidden
        />
      )}
      <aside
        className={`static inset-y-0 left-0 z-50 flex h-screen w-[260px] min-w-[260px] flex-col overflow-y-auto border-r border-gray-200 bg-[#f8f8f8] px-5 py-8 transition-transform lg:sticky lg:top-0 lg:z-auto lg:translate-x-0 ${
          open ? 'translate-x-0' : '-translate-x-full'
        }`}
      >
        <div className="mb-9 flex items-center gap-2.5 font-bold text-gray-900">
          <Globe />
          <span className="text-[32px] font-bold">TripNest</span>
        </div>

        <NavList items={PRIMARY_NAV} onNavigate={onClose} />
        <div className="my-5 h-px bg-gray-200 " />
        <NavList items={SECONDARY_NAV} onNavigate={onClose} />

        <div className="mt-auto">
          <div className="my-5 h-px bg-gray-200" />
          <NavList items={BOTTOM_NAV} onNavigate={onClose} />
        </div>
      </aside>
    </>
  );
}
