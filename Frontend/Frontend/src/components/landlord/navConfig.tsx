import type { ReactNode } from 'react';
import {
  HomeIcon, KeyIcon, MessageIcon, CalendarIcon, CardIcon, UsersIcon, StarIcon,
  SettingsIcon, HelpIcon, CheckIcon, BadgeIcon, FileIcon, ClockIcon, ChatIcon,
} from '../tenant/icons';

export interface LandlordNavItem {
  label: string;
  path: string;
  icon: ReactNode;
  badge?: number;
  /** Match the route exactly (used for the index route). */
  end?: boolean;
}

export interface LandlordNavGroup {
  heading?: string;
  items: LandlordNavItem[];
}

export const LANDLORD_NAV: LandlordNavGroup[] = [
  {
    items: [
      { label: 'Overview', path: '/landlord', icon: <HomeIcon />, end: true },
      { label: 'Reservations', path: '/landlord/reservations', icon: <CheckIcon /> },
      { label: 'Calendar', path: '/landlord/calendar', icon: <CalendarIcon /> },
      { label: 'Pricing', path: '/landlord/pricing', icon: <BadgeIcon /> },
      { label: 'Statements', path: '/landlord/statements', icon: <FileIcon /> },
    ],
  },
  {
    heading: 'Manage',
    items: [
      { label: 'My Listings', path: '/landlord/listings', icon: <KeyIcon /> },
      { label: 'Messages', path: '/landlord/messages', icon: <ChatIcon /> },
      { label: 'Inquiries', path: '/landlord/inquiries', icon: <MessageIcon /> },
      { label: 'Bookings', path: '/landlord/bookings', icon: <ClockIcon /> },
      { label: 'Earnings', path: '/landlord/earnings', icon: <CardIcon /> },
      { label: 'Agreements', path: '/landlord/agreements', icon: <FileIcon /> },
    ],
  },
  {
    heading: 'People',
    items: [
      { label: 'Tenants', path: '/landlord/tenants', icon: <UsersIcon /> },
      { label: 'Reviews', path: '/landlord/reviews', icon: <StarIcon /> },
    ],
  },
  {
    heading: 'Account',
    items: [
      { label: 'Settings', path: '/landlord/settings', icon: <SettingsIcon /> },
      { label: 'Help & Support', path: '/landlord/help', icon: <HelpIcon /> },
    ],
  },
];

/** Flat list of all landlord nav items, for route generation. */
export const LANDLORD_NAV_ITEMS: LandlordNavItem[] = LANDLORD_NAV.flatMap((g) => g.items);
