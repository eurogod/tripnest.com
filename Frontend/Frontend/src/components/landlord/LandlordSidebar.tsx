import WorkspaceSidebar from '../workspace/WorkspaceSidebar';
import { LANDLORD_NAV } from './navConfig';

interface LandlordSidebarProps {
  open: boolean;
  onClose: () => void;
  /** Collapse the sidebar on large screens to give the content full width. */
  desktopHidden?: boolean;
}

export default function LandlordSidebar({ open, onClose, desktopHidden = false }: LandlordSidebarProps) {
  return (
    <WorkspaceSidebar
      nav={LANDLORD_NAV}
      roleLabel="Landlord"
      accountPath="/landlord/settings"
      open={open}
      onClose={onClose}
      desktopHidden={desktopHidden}
    />
  );
}
