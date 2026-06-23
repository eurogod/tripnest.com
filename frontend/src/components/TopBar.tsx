import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Logo } from './Logo';
import { Avatar, VerifiedBadge } from './badges';
import { Bell, Chat, Search as SearchIcon } from './icons';
import { Button } from './ui';
import { useAuth, roleHome } from '@/auth/AuthContext';
import { useUnreadCount } from '@/lib/hooks';
import { UserRole, UserRoleLabel } from '@/lib/enums';

export function TopBar() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [q, setQ] = useState('');
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const { data: unread } = useUnreadCount();

  useEffect(() => {
    const onClick = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) setMenuOpen(false);
    };
    document.addEventListener('mousedown', onClick);
    return () => document.removeEventListener('mousedown', onClick);
  }, []);

  const submitSearch = (e: React.FormEvent) => {
    e.preventDefault();
    navigate(`/search?location=${encodeURIComponent(q.trim())}`);
  };

  return (
    <header className="sticky top-0 z-50 border-b border-line bg-white/90 backdrop-blur">
      <div className="container-tn flex h-16 items-center gap-4">
        <Logo />

        <form onSubmit={submitSearch} className="ml-2 hidden flex-1 justify-center md:flex">
          <div className="flex w-full max-w-md items-center gap-2 rounded-full border border-line bg-white px-2 py-1.5 shadow-card transition focus-within:shadow-soft">
            <SearchIcon className="ml-2 h-4 w-4 text-muted" />
            <input
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder="Search location, property ID or keyword…"
              className="w-full bg-transparent text-sm outline-none placeholder:text-muted/70"
            />
            <button className="btn-primary btn-sm rounded-full" type="submit">
              Search
            </button>
          </div>
        </form>

        <div className="ml-auto flex items-center gap-1.5">
          {user ? (
            <>
              <Link to="/messages" className="relative grid h-10 w-10 place-items-center rounded-full hover:bg-black/5" aria-label="Messages">
                <Chat className="h-5 w-5" />
              </Link>
              <Link to="/notifications" className="relative grid h-10 w-10 place-items-center rounded-full hover:bg-black/5" aria-label="Notifications">
                <Bell className="h-5 w-5" />
                {!!unread?.count && (
                  <span className="absolute right-1.5 top-1.5 grid h-4 min-w-4 place-items-center rounded-full bg-danger px-1 text-[10px] font-bold text-white">
                    {unread.count > 9 ? '9+' : unread.count}
                  </span>
                )}
              </Link>
              <div className="relative" ref={menuRef}>
                <button
                  onClick={() => setMenuOpen((o) => !o)}
                  className="ml-1 flex items-center gap-2 rounded-full border border-line p-1 pl-3 hover:shadow-card"
                >
                  <span className="hidden text-sm font-semibold sm:block">{user.fullName.split(' ')[0]}</span>
                  <Avatar name={user.fullName} size={32} />
                </button>
                {menuOpen && (
                  <div className="absolute right-0 mt-2 w-60 animate-scale-in overflow-hidden rounded-xl border border-line bg-white py-1 shadow-lg">
                    <div className="px-4 py-3">
                      <p className="font-bold">{user.fullName}</p>
                      <p className="text-xs text-muted">{UserRoleLabel[user.role]}</p>
                      <div className="mt-1.5 flex flex-wrap gap-1">
                        {user.isVerified ? <VerifiedBadge size="sm" /> : null}
                      </div>
                    </div>
                    <div className="border-t border-line py-1">
                      <MenuLink to={roleHome(user.role)} onClick={() => setMenuOpen(false)}>Dashboard</MenuLink>
                      {user.role === UserRole.Tenant && <MenuLink to="/dashboard" onClick={() => setMenuOpen(false)}>My trips</MenuLink>}
                      <MenuLink to="/wishlist" onClick={() => setMenuOpen(false)}>Saved</MenuLink>
                      <MenuLink to="/messages" onClick={() => setMenuOpen(false)}>Messages</MenuLink>
                      <MenuLink to="/verification" onClick={() => setMenuOpen(false)}>Verification</MenuLink>
                      <MenuLink to="/id-card" onClick={() => setMenuOpen(false)}>My TripNest ID</MenuLink>
                      <MenuLink to="/settings" onClick={() => setMenuOpen(false)}>Settings</MenuLink>
                    </div>
                    <div className="border-t border-line py-1">
                      <button
                        onClick={() => { setMenuOpen(false); logout(); navigate('/'); }}
                        className="w-full px-4 py-2 text-left text-sm font-semibold text-danger hover:bg-black/5"
                      >
                        Log out
                      </button>
                    </div>
                  </div>
                )}
              </div>
            </>
          ) : (
            <>
              <Link to="/login" className="btn-ghost btn-sm hidden sm:inline-flex">Log in</Link>
              <Button size="sm" onClick={() => navigate('/signup')}>Sign up</Button>
            </>
          )}
        </div>
      </div>
    </header>
  );
}

function MenuLink({ to, children, onClick }: { to: string; children: React.ReactNode; onClick: () => void }) {
  return (
    <Link to={to} onClick={onClick} className="block px-4 py-2 text-sm font-medium hover:bg-black/5">
      {children}
    </Link>
  );
}
