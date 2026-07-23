import type { ReactNode } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { HexIcon } from '../tenant/icons';
import { signOut, useSession } from '../../store/authStore';
import { useT } from '../../lib/i18n';
import Avatar from '../ui/Avatar';

export interface WorkspaceNavItem {
  label: string;
  /** Destination route; omit for action items such as `logout`. */
  path?: string;
  icon: ReactNode;
  badge?: number;
  /** Match the route exactly (used for the index route). */
  end?: boolean;
  /** Render as a button that signs the user out instead of a nav link. */
  logout?: boolean;
}

export interface WorkspaceNavGroup {
  heading?: string;
  items: WorkspaceNavItem[];
}

interface WorkspaceSidebarProps {
  nav: WorkspaceNavGroup[];
  /** Shown under the user's name in the footer card, e.g. "Landlord". */
  roleLabel: string;
  /** Where the footer profile card links to. */
  accountPath: string;
  open: boolean;
  onClose: () => void;
  /** Collapse the sidebar on large screens to give the content full width. */
  desktopHidden?: boolean;
}

const baseItem =
  'flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm no-underline transition-all';
const inactiveItem = 'font-medium text-gray-500 hover:bg-black/5 hover:text-ink';
const activeItem = 'bg-white font-semibold text-ink shadow-[0_1px_4px_rgba(0,0,0,0.08)] ring-1 ring-black/[0.04]';

/** Landlord-style workspace sidebar, shared by every role dashboard. */
export default function WorkspaceSidebar({
  nav, roleLabel, accountPath, open, onClose, desktopHidden = false,
}: WorkspaceSidebarProps) {
  const session = useSession();
  const navigate = useNavigate();
  const t = useT();
  const name = session?.name ?? t('Guest');

  const handleLogout = () => {
    onClose();
    signOut();
    navigate('/welcome');
  };
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
        className={`fixed inset-y-0 left-0 z-50 flex h-screen w-[260px] min-w-[260px] flex-col overflow-y-auto bg-[#f7f7f5] px-4 py-6 transition-transform lg:z-auto ${
          open ? 'translate-x-0' : '-translate-x-full'
        } ${desktopHidden ? 'lg:hidden' : 'lg:sticky lg:top-0 lg:translate-x-0'}`}
      >
        <div className="mb-8 flex items-center gap-2 px-2">
          <HexIcon size={24} className="text-ink" />
          <p className="text-xl font-bold tracking-tight text-ink">TripNest</p>
        </div>

        <nav className="flex flex-col">
          {nav.map((group, gi) => (
            <div key={gi} className={gi > 0 ? 'mt-4 border-t border-gray-200 pt-4' : ''}>
              <div className="flex flex-col gap-0.5">
                {group.items.map((item) => (
                  item.logout ? (
                    <button
                      key={item.label}
                      type="button"
                      onClick={handleLogout}
                      className={`${baseItem} w-full text-left font-medium text-rose-600 hover:bg-rose-50`}
                    >
                      <span className="flex shrink-0 items-center justify-center">{item.icon}</span>
                      <span className="flex-1">{t(item.label)}</span>
                    </button>
                  ) : (
                    <NavLink
                      key={item.path}
                      to={item.path!}
                      end={item.end}
                      onClick={onClose}
                      className={({ isActive }) =>
                        `${baseItem} ${isActive ? activeItem : inactiveItem}`
                      }
                    >
                      <span className="flex shrink-0 items-center justify-center">{item.icon}</span>
                      <span className="flex-1">{t(item.label)}</span>
                      {item.badge != null && (
                        <span className="rounded-full bg-ink px-2 py-0.5 text-[11px] font-semibold text-white">
                          {item.badge}
                        </span>
                      )}
                    </NavLink>
                  )
                ))}
              </div>
            </div>
          ))}
        </nav>

        <NavLink
          to={accountPath}
          onClick={onClose}
          className="mt-auto flex w-full items-center gap-3 rounded-xl px-2 py-2.5 text-left no-underline transition-colors hover:bg-black/5"
        >
          <Avatar name={name} size={36} />
          <span className="min-w-0 flex-1">
            <span className="block truncate text-sm font-semibold text-ink">{name}</span>
            <span className="block text-xs text-muted">{t(roleLabel)}</span>
          </span>
        </NavLink>
      </aside>
    </>
  );
}
