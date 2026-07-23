import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useSession, type Role } from '../store/authStore';
import { homeForRole } from '../lib/roleHome';

/**
 * Gate protected routes. No session → onboarding. When `role` is given, a
 * signed-in user of another role is sent to their own home surface, so login
 * decides which side of the app someone sees. `verified` additionally sends
 * users without a confirmed Ghana Card identity through /get-verified first
 * (their workspace APIs are [RequireVerified] server-side anyway).
 */
export default function RequireAuth({
  children,
  role,
  verified = false,
}: {
  children: ReactNode;
  role?: Role;
  verified?: boolean;
}) {
  const session = useSession();
  if (!session) return <Navigate to="/welcome" replace />;
  if (role && session.role !== role) {
    return <Navigate to={homeForRole(session.role)} replace />;
  }
  if (verified && !session.verified) {
    return <Navigate to="/get-verified" replace />;
  }
  return <>{children}</>;
}
