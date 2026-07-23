import { useState } from 'react';
import { Outlet } from 'react-router-dom';
import WorkspaceSidebar, { type WorkspaceNavGroup } from '../components/workspace/WorkspaceSidebar';
import TopBar from '../components/tenant/TopBar';
import Footer from '../components/tenant/Footer';
import ChatButton from '../components/tenant/ChatButton';
import { useIsDesktop } from '../hooks/useIsDesktop';

interface RoleWorkspaceLayoutProps {
  nav: WorkspaceNavGroup[];
  roleLabel: string;
  accountPath: string;
}

/** Landlord-style shell for the agent / caretaker / admin workspaces. */
export default function RoleWorkspaceLayout({ nav, roleLabel, accountPath }: RoleWorkspaceLayoutProps) {
  const [mobileOpen, setMobileOpen] = useState(false);
  const [desktopHidden, setDesktopHidden] = useState(false);
  const isDesktop = useIsDesktop();
  const sidebarOpen = isDesktop ? !desktopHidden : mobileOpen;

  // The hamburger collapses the sidebar on large screens (more room for
  // content) and toggles the drawer on small ones.
  const toggleSidebar = () => {
    if (isDesktop) {
      setDesktopHidden((v) => !v);
    } else {
      setMobileOpen((v) => !v);
    }
  };

  return (
    <div className="flex min-h-screen bg-white">
      <WorkspaceSidebar
        nav={nav}
        roleLabel={roleLabel}
        accountPath={accountPath}
        open={mobileOpen}
        onClose={() => setMobileOpen(false)}
        desktopHidden={desktopHidden}
      />
      <div className="flex min-w-0 flex-1 flex-col">
        <TopBar onMenu={toggleSidebar} sidebarOpen={sidebarOpen} />
        <main className="flex-1 p-4 sm:p-8">
          <Outlet />
        </main>
        <Footer />
      </div>
      <ChatButton />
    </div>
  );
}
