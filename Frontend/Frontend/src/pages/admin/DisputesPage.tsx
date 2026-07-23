import { useState } from 'react';
import ClaimsReviewSection from '../../components/admin/ClaimsReviewSection';
import { getDisputedEscrows, resolveDispute, refundEscrow } from '../../api/adminWorkspace';
import type { EscrowResponseDto } from '../../api/escrow';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import Badge from '../../components/ui/Badge';
import { formatCurrency } from '../../lib/format';
import { formatIsoDate } from '../../api/backend';

export default function DisputesPage() {
  const state = useAsync(getDisputedEscrows);

  return (
    <div>
      <h1 className="text-3xl font-bold tracking-tight text-ink">Disputes</h1>
      <p className="mt-1 mb-6 text-sm text-muted">
        Escrows a party has disputed. Approving releases the funds to the landlord; declining refunds the tenant.
      </p>
      <AsyncBoundary
        state={state}
        errorMessage="Failed to load the dispute queue."
        emptyMessage="No open disputes — nothing needs your attention."
        isEmpty={(rows) => rows.length === 0}
      >
        {(rows) => <Queue initial={rows} />}
      </AsyncBoundary>

      <ClaimsReviewSection />
    </div>
  );
}

function Queue({ initial }: { initial: EscrowResponseDto[] }) {
  const [rows, setRows] = useState(initial);
  const [busy, setBusy] = useState(false);
  // Row currently showing the refund-reason input.
  const [refunding, setRefunding] = useState<string | null>(null);
  const [reason, setReason] = useState('');

  const act = async (escrowId: string, action: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await action();
      setRows((rs) => rs.filter((r) => r.escrowId !== escrowId));
      setRefunding(null);
      setReason('');
    } catch {
      // resolution didn't go through — keep the row so it can be retried
    } finally {
      setBusy(false);
    }
  };

  if (rows.length === 0) return <p className="text-muted">No open disputes — nothing needs your attention.</p>;

  return (
    <div className="space-y-4">
      {rows.map((r) => (
        <Card key={r.escrowId} className="p-5">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center">
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2">
                <h3 className="font-semibold text-ink">{formatCurrency(r.amount)}</h3>
                <Badge tone="red">Disputed</Badge>
              </div>
              <p className="mt-1 text-xs text-muted">
                Escrow {r.escrowId.slice(0, 8)} · booking {r.bookingId.slice(0, 8)} · opened {formatIsoDate(r.createdAt)}
                {r.paymentReference && ` · ref ${r.paymentReference}`}
              </p>
            </div>
            <div className="flex shrink-0 gap-2">
              <Button size="sm" disabled={busy} onClick={() => act(r.escrowId, () => resolveDispute(r.escrowId, true))}>
                Release to landlord
              </Button>
              <Button size="sm" variant="ghost" disabled={busy} onClick={() => act(r.escrowId, () => resolveDispute(r.escrowId, false))}>
                Refund tenant
              </Button>
              <Button
                size="sm"
                variant="ghost"
                className="text-rose-600 hover:bg-rose-50"
                disabled={busy}
                onClick={() => setRefunding(refunding === r.escrowId ? null : r.escrowId)}
              >
                Refund with reason
              </Button>
            </div>
          </div>
          {refunding === r.escrowId && (
            <div className="mt-4 flex flex-col gap-2 border-t border-gray-100 pt-4 sm:flex-row">
              <input
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                placeholder="Refund reason (recorded on the escrow)"
                className="flex-1 rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-brand"
              />
              <Button
                size="sm"
                variant="dark"
                disabled={busy || !reason.trim()}
                onClick={() => act(r.escrowId, () => refundEscrow(r.escrowId, reason.trim()))}
              >
                Confirm refund
              </Button>
            </div>
          )}
        </Card>
      ))}
    </div>
  );
}
