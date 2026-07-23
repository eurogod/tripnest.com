import { useState } from 'react';
import {
  getPendingWalkthroughs, reviewWalkthrough, type PropertyWalkthroughStatusDto,
} from '../../api/walkthroughs';
import { getListingProperty } from '../../api/listings';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import Badge from '../../components/ui/Badge';

interface Row extends PropertyWalkthroughStatusDto {
  propertyTitle: string;
}

// Shared by the agent and admin workspaces — both roles review walkthroughs.
export default function WalkthroughReviewPage() {
  const state = useAsync(async (): Promise<Row[]> => {
    const pending = await getPendingWalkthroughs();
    // Pending listings are still Draft, so the public active-listings feed can't
    // name them — fetch each property record directly instead.
    return Promise.all(pending.map(async (w) => {
      const propertyTitle = await getListingProperty(w.propertyId)
        .then((p) => p.title)
        .catch(() => `Property ${w.propertyId.slice(0, 8)}`);
      return { ...w, propertyTitle };
    }));
  });

  return (
    <div>
      <h1 className="text-3xl font-bold tracking-tight text-ink">Walkthrough review</h1>
      <p className="mt-1 mb-6 text-sm text-muted">Property walkthrough videos awaiting a verification decision.</p>
      <AsyncBoundary
        state={state}
        errorMessage="Failed to load the review queue."
        emptyMessage="No walkthroughs waiting for review."
        isEmpty={(rows) => rows.length === 0}
      >
        {(rows) => <Queue initial={rows} />}
      </AsyncBoundary>
    </div>
  );
}

function Queue({ initial }: { initial: Row[] }) {
  const [rows, setRows] = useState(initial);
  // Row currently showing the rejection-reason input.
  const [rejecting, setRejecting] = useState<string | null>(null);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);

  const decide = async (propertyId: string, approved: boolean) => {
    setBusy(true);
    try {
      await reviewWalkthrough(propertyId, approved, approved ? undefined : reason.trim() || undefined);
      setRows((rs) => rs.filter((r) => r.propertyId !== propertyId));
      setRejecting(null);
      setReason('');
    } catch {
      // decision didn't go through — keep the row so it can be retried
    } finally {
      setBusy(false);
    }
  };

  if (rows.length === 0) return <p className="text-muted">No walkthroughs waiting for review.</p>;

  return (
    <div className="space-y-4">
      {rows.map((r) => (
        <Card key={r.propertyId} className="p-5">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center">
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2">
                <h3 className="font-semibold text-ink">{r.propertyTitle}</h3>
                <Badge tone="amber">Pending review</Badge>
              </div>
              <p className="mt-1 text-xs text-muted">
                {r.videoPath ? r.videoPath.split('/').pop() : 'No video file recorded'} · video preview unavailable until uploads are served
              </p>
            </div>
            <div className="flex shrink-0 gap-2">
              <Button size="sm" disabled={busy} onClick={() => decide(r.propertyId, true)}>Approve</Button>
              <Button
                size="sm"
                variant="ghost"
                className="text-rose-600 hover:bg-rose-50"
                disabled={busy}
                onClick={() => setRejecting(rejecting === r.propertyId ? null : r.propertyId)}
              >
                Reject
              </Button>
            </div>
          </div>
          {rejecting === r.propertyId && (
            <div className="mt-4 flex flex-col gap-2 border-t border-gray-100 pt-4 sm:flex-row">
              <input
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                placeholder="Reason shown to the landlord (optional)"
                className="flex-1 rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-brand"
              />
              <Button size="sm" variant="dark" disabled={busy} onClick={() => decide(r.propertyId, false)}>
                Confirm rejection
              </Button>
            </div>
          )}
        </Card>
      ))}
    </div>
  );
}
