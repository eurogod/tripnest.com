import type { ReactNode } from 'react';
import {
  HomeIcon, SearchIcon, HeartIcon, CalendarIcon, MessageIcon, FileIcon, CardIcon,
  BellIcon, UsersIcon, SparkleIcon, ToolIcon, UserCheckIcon, UserIcon, SettingsIcon, HelpIcon,
  MapPinIcon, GridIcon,
} from './icons';

export interface TenantNavItem {
  label: string;
  path: string;
  icon: ReactNode;
  badge?: number;
}

export interface TenantNavGroup {
  heading?: string;
  items: TenantNavItem[];
}

export const TENANT_NAV: TenantNavGroup[] = [
  {
    items: [
      { label: 'Home', path: '/', icon: <HomeIcon /> },
      { label: 'Dashboard', path: '/overview', icon: <GridIcon /> },
    ],
  },
  {
    heading: 'Main',
    items: [
      { label: 'Search Properties', path: '/search', icon: <SearchIcon /> },
      { label: 'Nearby', path: '/nearby', icon: <MapPinIcon /> },
      { label: 'Saved Listings', path: '/saved', icon: <HeartIcon /> },
      { label: 'Bookings', path: '/bookings', icon: <CalendarIcon /> },
      { label: 'Messages', path: '/messages', icon: <MessageIcon /> },
      { label: 'Agreements', path: '/agreements', icon: <FileIcon /> },
      { label: 'Payments', path: '/payments', icon: <CardIcon /> },
      { label: 'Notifications', path: '/notifications', icon: <BellIcon /> },
    ],
  },
  {
    heading: 'Services',
    items: [
      { label: 'Caretakers', path: '/caretakers', icon: <UsersIcon /> },
      { label: 'House Help', path: '/house-help', icon: <SparkleIcon /> },
      { label: 'Maintenance', path: '/maintenance', icon: <ToolIcon /> },
      { label: 'Roommates', path: '/roommates', icon: <UsersIcon /> },
      { label: 'Agents', path: '/agents', icon: <UserCheckIcon /> },
    ],
  },
  {
    heading: 'Account',
    items: [
      { label: 'Profile', path: '/profile', icon: <UserIcon /> },
      { label: 'Settings', path: '/settings', icon: <SettingsIcon /> },
      { label: 'Help & Support', path: '/help', icon: <HelpIcon /> },
    ],
  },
];

/** Flat list of all tenant nav items, for route generation. */
export const TENANT_NAV_ITEMS: TenantNavItem[] = TENANT_NAV.flatMap((g) => g.items);

// Pages whose APIs are Tenant-role-only (personal dashboard, maintenance/mine).
// Routes gate them with RequireAuth role="tenant" and the sidebar hides them
// from other signed-in roles so nobody clicks into a redirect.
export const TENANT_ROLE_PATHS = new Set(['/overview', '/maintenance']);
