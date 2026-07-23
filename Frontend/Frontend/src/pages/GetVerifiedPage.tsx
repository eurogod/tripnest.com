import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import IdentityVerification from '../components/IdentityVerification';
import type { VerificationState } from '../api/verification';
import { useSession } from '../store/authStore';
import { homeForRole } from '../lib/roleHome';
import { HexIcon } from '../components/tenant/icons';
import Button from '../components/ui/Button';
import Card from '../components/ui/Card';

// Roles whose workspaces are gated on a verified identity, mirroring Core's
// RequireVerified filter: Landlord/Agent/Caretaker. Tenants and Admins are exempt.
const VERIFIED_ONLY_ROLES = new Set(['landlord', 'agent', 'caretaker']);

/**
 * Standalone Ghana Card verification step. New sign-ups land here from
 * WelcomePage; unverified agents/caretakers/admins are bounced here by the
 * workspace routes and can only continue once the check passes.
 */
export default function GetVerifiedPage() {
  const session = useSession();
  const navigate = useNavigate();
  const [state, setState] = useState<VerificationState>('not-started');

  const role = session?.role ?? 'tenant';
  const home = homeForRole(role);
  const mustVerify = VERIFIED_ONLY_ROLES.has(role);

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="border-b border-gray-200 bg-white">
        <div className="mx-auto flex max-w-3xl items-center gap-2.5 px-6 py-4">
          <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-brand text-white">
            <HexIcon size={20} />
          </span>
          <div className="leading-tight">
            <p className="text-lg font-bold text-ink">TripNest</p>
            <p className="text-[11px] text-muted">Identity verification</p>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-3xl px-6 py-10">
        <h1 className="text-3xl font-bold tracking-tight text-ink">Verify your identity</h1>
        <p className="mt-2 text-muted">
          {mustVerify
            ? `${role.charAt(0).toUpperCase() + role.slice(1)} accounts must verify with their Ghana Card before using their workspace.`
            : 'Verify with your Ghana Card to unlock bookings, hosting and payments across TripNest.'}
        </p>

        <Card className="mt-8 p-6">
          <IdentityVerification
            onStateChange={setState}
            // Skipping is only offered to roles Core doesn't gate on verification.
            onSkip={!mustVerify && state !== 'verified'
              ? () => navigate(home, { replace: true })
              : undefined}
          />
        </Card>

        <div className="mt-6 flex items-center gap-4">
          {state === 'verified' && (
            <Button onClick={() => navigate(home, { replace: true })}>
              Continue to TripNest
            </Button>
          )}
          {mustVerify && state !== 'verified' && (
            <p className="text-sm text-muted">
              You'll be able to continue as soon as your identity is confirmed.
            </p>
          )}
        </div>
      </main>
    </div>
  );
}
