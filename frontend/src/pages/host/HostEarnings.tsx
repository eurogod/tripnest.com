import { useQuery } from '@tanstack/react-query';
import { dashboardApi } from '@/lib/services';
import { PageHeader, StatGrid, StatCard, SectionCard } from '@/components/dashboard';
import { Cash, Shield, Check } from '@/components/icons';
import { money } from '@/lib/format';
import { useAuth } from '@/auth/AuthContext';

type Raw = Record<string, unknown>;
const num = (o: Raw | undefined, ...keys: string[]): number => {
  for (const k of keys) if (typeof o?.[k] === 'number') return o[k] as number;
  return 0;
};

export default function HostEarnings() {
  const { user } = useAuth();
  const query = useQuery({ queryKey: ['landlord-earnings'], queryFn: dashboardApi.landlordEarnings, enabled: !!user });
  const d = query.data as Raw | undefined;

  const released = num(d, 'totalReleased', 'released', 'totalEarnings', 'earnings');
  const held = num(d, 'totalEscrowHeld', 'inEscrow', 'held', 'escrowHeld');
  const pending = num(d, 'pending', 'totalPending');

  return (
    <div>
      <PageHeader title="Earnings & escrow" subtitle="How your money moves — held safely until check-in, then released to you." />

      <StatGrid>
        <StatCard label="Released to you" value={money(released)} icon={<Check className="h-4 w-4" />} tone="success" />
        <StatCard label="Held in escrow" value={money(held)} icon={<Shield className="h-4 w-4" />} tone="gold" />
        <StatCard label="Pending" value={money(pending)} icon={<Cash className="h-4 w-4" />} tone="muted" />
        <StatCard label="Lifetime" value={money(released + held + pending)} icon={<Cash className="h-4 w-4" />} />
      </StatGrid>

      <div className="mt-6">
        <SectionCard title="How escrow protects everyone">
          <ul className="space-y-3 text-sm text-muted">
            <li className="flex gap-3">
              <span className="grid h-6 w-6 shrink-0 place-items-center rounded-full bg-brand-50 text-xs font-bold text-brand-700">1</span>
              A tenant pays — the money is held securely in escrow, not sent to you yet.
            </li>
            <li className="flex gap-3">
              <span className="grid h-6 w-6 shrink-0 place-items-center rounded-full bg-brand-50 text-xs font-bold text-brand-700">2</span>
              On a verified check-in, funds are released to your account automatically.
            </li>
            <li className="flex gap-3">
              <span className="grid h-6 w-6 shrink-0 place-items-center rounded-full bg-brand-50 text-xs font-bold text-brand-700">3</span>
              If something’s wrong, a dispute is opened and our team reviews it fairly.
            </li>
          </ul>
        </SectionCard>
      </div>
    </div>
  );
}
