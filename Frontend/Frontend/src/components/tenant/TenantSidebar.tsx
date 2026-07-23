import { NavLink } from 'react-router-dom';
import { TENANT_NAV, TENANT_ROLE_PATHS } from './navConfig';
import { HexIcon, ChevronDownIcon, GridIcon } from './icons';
import { useSession } from '../../store/authStore';
import { homeForRole, profilePathForRole } from '../../lib/roleHome';
import { getUnreadCountCached } from '../../api/notifications';
import { useAsync } from '../../hooks/useAsync';
import { useT } from '../../lib/i18n';
import Avatar from '../ui/Avatar';
import Button from '../ui/Button';

interface TenantSidebarProps {
  open: boolean;
  onClose: () => void;
  /** Collapse the sidebar on large screens to give the content full width. */
  desktopHidden?: boolean;
}

const baseItem =
  'flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium no-underline transition-colors';
const inactiveItem = 'text-gray-600 hover:bg-gray-100 hover:text-gray-900';
const activeItem = 'bg-brand-50 text-brand';

export default function TenantSidebar({ open, onClose, desktopHidden = false }: TenantSidebarProps) {
  const session = useSession();
  const t = useT();
  const name = session?.name ?? t('Guest');

  // Non-tenant roles browsing the marketplace don't get the tenant-role-only
  // pages (their APIs 403 for them) — they get a link back to their workspace.
  const isOtherRole = session != null && session.role !== 'tenant';
  const nav = isOtherRole
    ? TENANT_NAV
        .map((g) => ({ ...g, items: g.items.filter((i) => !TENANT_ROLE_PATHS.has(i.path)) }))
        .filter((g) => g.items.length > 0)
    : TENANT_NAV;
  const roleLabel = session
    ? t(session.role.charAt(0).toUpperCase() + session.role.slice(1))
    : t('Browse as guest');

  // Live unread count for the Notifications badge (signed-in users only).
  const unread = useAsync(
    () => (session ? getUnreadCountCached(session.userId) : Promise.resolve(0)),
    [session?.userId],
  );
  const badgeFor = (path: string): number | undefined =>
    path === '/notifications' && unread.data ? unread.data : undefined;

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
        className={`fixed inset-y-0 left-0 z-50 flex h-screen w-[260px] min-w-[260px] flex-col overflow-y-auto border-r border-gray-200 bg-white px-4 py-6 transition-transform lg:z-auto ${
          open ? 'translate-x-0' : '-translate-x-full'
        } ${desktopHidden ? 'lg:hidden' : 'lg:sticky lg:top-0 lg:translate-x-0'}`}
      >
        <div className="mb-6 flex items-center gap-2.5 px-2">
          <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-brand text-white">
            <HexIcon size={20} />
          </span>
          <div className="leading-tight">
            <p className="text-lg font-bold text-ink">TripNest</p>
            <p className="text-[11px] text-muted">Find | Stay | Thrive</p>
          </div>
        </div>

        <nav className="flex flex-col gap-1">
          {isOtherRole && (
            <NavLink
              to={homeForRole(session.role)}
              onClick={onClose}
              className={`${baseItem} ${inactiveItem}`}
            >
              <span className="flex shrink-0 items-center justify-center"><GridIcon /></span>
              <span className="flex-1">{t('My workspace')}</span>
            </NavLink>
          )}
          {nav.map((group, gi) => (
            <div key={gi}>
              {group.heading && (
                <p className="px-3 pt-5 pb-2 text-[11px] font-semibold uppercase tracking-wider text-gray-400">
                  {t(group.heading)}
                </p>
              )}
              {group.items.map((item) => (
                <NavLink
                  key={item.path}
                  to={item.path}
                  end={item.path === '/'}
                  onClick={onClose}
                  className={({ isActive }) =>
                    `${baseItem} ${isActive ? activeItem : inactiveItem}`
                  }
                >
                  <span className="flex shrink-0 items-center justify-center">{item.icon}</span>
                  <span className="flex-1">{t(item.label)}</span>
                  {(item.badge ?? badgeFor(item.path)) != null && (
                    <span className="rounded-full bg-brand px-2 py-0.5 text-[11px] font-semibold text-white">
                      {item.badge ?? badgeFor(item.path)}
                    </span>
                  )}
                </NavLink>
              ))}
            </div>
          ))}
        </nav>

        {!session && (
          <div className="mt-6 rounded-xl bg-brand p-4 text-white">
            <p className="font-semibold">{t('Become a Host')}</p>
            <p className="mt-1 text-xs text-white/80">
              {t('Create a landlord account and start earning today!')}
            </p>
            <NavLink to="/welcome" onClick={onClose} className="no-underline">
              <Button className="mt-3 bg-white! text-brand! hover:bg-white/90!" size="sm">
                {t('Get Started')}
              </Button>
            </NavLink>
          </div>
        )}

        <NavLink
          to={session ? profilePathForRole(session.role) : '/welcome'}
          onClick={onClose}
          className="mt-4 flex w-full items-center gap-3 rounded-xl border border-gray-200 px-3 py-2.5 text-left no-underline"
        >
          <Avatar name={name} size={36} />
          <span className="min-w-0 flex-1">
            <span className="block truncate text-sm font-semibold text-ink">{session ? name : t('Sign in')}</span>
            <span className="block text-xs text-muted">{roleLabel}</span>
          </span>
          <ChevronDownIcon size={16} className="text-muted" />
        </NavLink>
      </aside>
    </>
  );
}
