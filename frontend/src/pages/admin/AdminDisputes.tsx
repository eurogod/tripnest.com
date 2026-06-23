import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { dashboardApi, escrowApi } from '@/lib/services';
import { PageHeader, StatGrid, StatCard, SectionCard } from '@/components/dashboard';
import { Button, Input } from '@/components/ui';
import { EscrowStatusPill } from '@/components/badges';
import { Cash } from '@/components/icons';
import { money, fmtDate } from '@/lib/format';
import { useAuth } from '@/auth/AuthContext';
import { useToast } from '@/components/Toast';
import { ApiError } from '@/lib/api';
import { EscrowStatus } from '@/lib/enums';
import type { Escrow } from '@/types/api';

export default function AdminDisputes() {
  const { user } = useAuth();
  const qc = useQueryClient();
  const toast = useToast();
  const stats = useQuery({ queryKey: ['admin-stats'], queryFn: dashboardApi.adminStats, enabled: !!user });

  const [id, setId] = useState('');
  const [escrow, setEscrow] = useState<Escrow | null>(null);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState<string | null>(null);

  async function lookup() {
    setLoading(true);
    setEscrow(null);
    try {
      setEscrow(await escrowApi.get(id.trim()));
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Escrow not found');
    } finally {
      setLoading(false);
    }
  }

  async function resolve(approved: boolean) {
    if (!escrow) return;
    setBusy(approved ? 'release' : 'refund');
    try {
      const updated = approved
        ? await escrowApi.resolveDispute(escrow.escrowId, true)
        : await escrowApi.refund(escrow.escrowId, 'Dispute resolved in tenant’s favour');
      setEscrow(updated);
      toast.success(approved ? 'Released to host' : 'Refunded to tenant');
      qc.invalidateQueries({ queryKey: ['admin-stats'] });
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Could not resolve dispute');
    } finally {
      setBusy(null);
    }
  }

  const s = stats.data;

  return (
    <div>
      <PageHeader title="Disputes & escrow" subtitle="Review escrow disputes and decide where the money goes — fairly." />

      <StatGrid>
        <StatCard label="Open disputes" value={s?.openDisputes ?? '—'} icon={<Cash className="h-4 w-4" />} tone={s && s.openDisputes > 0 ? 'danger' : 'muted'} />
        <StatCard label="Held in escrow" value={money(s?.totalEscrowHeld ?? 0)} icon={<Cash className="h-4 w-4" />} tone="gold" />
        <StatCard label="Released" value={money(s?.totalEscrowReleased ?? 0)} icon={<Cash className="h-4 w-4" />} tone="success" />
        <StatCard label="Bookings" value={s?.totalBookings ?? '—'} icon={<Cash className="h-4 w-4" />} />
      </StatGrid>

      <div className="mt-6 max-w-xl">
        <SectionCard title="Resolve a dispute">
          <p className="mb-4 text-sm text-muted">Enter the escrow ID from the dispute to review and resolve it.</p>
          <div className="flex gap-2">
            <Input placeholder="Escrow ID" value={id} onChange={(e) => setId(e.target.value)} />
            <Button loading={loading} disabled={!id.trim()} onClick={lookup}>
              Look up
            </Button>
          </div>

          {escrow && (
            <div className="mt-5 rounded-xl border border-line p-4">
              <div className="flex items-center justify-between">
                <span className="text-sm text-muted">Amount</span>
                <span className="text-lg font-extrabold">{money(escrow.amount)}</span>
              </div>
              <div className="mt-2 flex items-center justify-between">
                <span className="text-sm text-muted">Status</span>
                <EscrowStatusPill status={escrow.status} />
              </div>
              <div className="mt-2 flex items-center justify-between">
                <span className="text-sm text-muted">Created</span>
                <span className="text-sm">{fmtDate(escrow.createdAt)}</span>
              </div>

              {escrow.status === EscrowStatus.Disputed ? (
                <div className="mt-4 flex gap-3">
                  <Button block loading={busy === 'release'} onClick={() => resolve(true)}>
                    Release to host
                  </Button>
                  <Button variant="danger" block loading={busy === 'refund'} onClick={() => resolve(false)}>
                    Refund tenant
                  </Button>
                </div>
              ) : (
                <p className="mt-4 rounded-lg bg-surface p-3 text-sm text-muted">
                  This escrow isn’t in a disputed state, so there’s nothing to resolve.
                </p>
              )}
            </div>
          )}
        </SectionCard>
      </div>
    </div>
  );
}
