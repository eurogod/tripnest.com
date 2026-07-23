import {
  HomeIcon, CalendarIcon, UserIcon, FileIcon, ToolIcon,
  MessageIcon, ClockIcon, ShieldIcon, ChatIcon, LogOutIcon,
} from '../tenant/icons';
import type { WorkspaceNavGroup } from './WorkspaceSidebar';

// Sidebar navs for the non-landlord workspaces. Same shape as LANDLORD_NAV;
// paths must match the nested routes in App.tsx.

export const AGENT_NAV: WorkspaceNavGroup[] = [
  {
    items: [
      { label: 'Overview', path: '/agent', icon: <HomeIcon />, end: true },
      { label: 'Walkthrough review', path: '/agent/walkthroughs', icon: <FileIcon /> },
      { label: 'Viewing requests', path: '/agent/viewings', icon: <CalendarIcon /> },
      { label: 'Messages', path: '/agent/messages', icon: <ChatIcon /> },
    ],
  },
  {
    heading: 'Account',
    items: [
      { label: 'My profile', path: '/agent/profile', icon: <UserIcon /> },
    ],
  },
];

export const CARETAKER_NAV: WorkspaceNavGroup[] = [
  {
    items: [
      { label: 'Overview', path: '/caretaker', icon: <HomeIcon />, end: true },
      { label: 'Service requests', path: '/caretaker/requests', icon: <ToolIcon /> },
      { label: 'Messages', path: '/caretaker/messages', icon: <ChatIcon /> },
    ],
  },
];

export const ADMIN_NAV: WorkspaceNavGroup[] = [
  {
    items: [
      { label: 'Overview', path: '/admin', icon: <HomeIcon />, end: true },
      { label: 'Disputes', path: '/admin/disputes', icon: <ShieldIcon /> },
      { label: 'Support tickets', path: '/admin/tickets', icon: <MessageIcon /> },
      { label: 'Walkthrough review', path: '/admin/walkthroughs', icon: <FileIcon /> },
      { label: 'Messages', path: '/admin/messages', icon: <ChatIcon /> },
      { label: 'Audit logs', path: '/admin/audit', icon: <ClockIcon /> },
      { label: 'Log out', logout: true, icon: <LogOutIcon /> },
    ],
  },
];
