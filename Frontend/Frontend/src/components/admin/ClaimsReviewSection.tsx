import { useEffect, useState } from 'react';
import {
  approveClaim, getClaimBrief, getClaimsForReview, rejectClaim, type AdminBrief, type DamageClaimDto,
} from '../../api/claims';
import Card from '../ui/Card';
import Button from '../ui/Button';
import Badge from '../ui/Badge';
import { formatCurrency } from '../../lib/format';

/**
 * Damage-claim review queue: both sides of each claim, an optional AI reading brief, and the
 * decision — approve (full or reduced) or reject. The human decides; the brief is reading speed.
 */
export default function ClaimsReviewSection() {
  const [claims, setClaims] = useState<DamageClaimDto[]>([]);
  const [briefs, setBriefs] = useState<Record<string, AdminBrief>>({});
  const [busyId, setBusyId] = useState<string | null>(null);
  const [amounts, setAmounts] = useState<Record<string, string>>({});
  const [notes, setNotes] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getClaimsForReview().then(setClaims).catch(() => setClaims([]));
  }, []);

  const loadBrief = async (id: string) => {
    setBusyId(id);
    try {
      setBriefs((b) => ({ ...b, [id]: undefined as unknown as AdminBrief }));
      const brief = await getClaimBrief(id);
      setBriefs((b) => ({ ...b, [id]: brief }));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Brief unavailable.');
    } finally {
      setBusyId(null);
    }
  };

  const decide = async (id: string, approve: boolean) => {
    setBusyId(id);
    setError(null);
    try {
      if (approve) {
        const amt = amounts[id] ? Number(amounts[id]) : undefined;
        await approveClaim(id, amt, notes[id] || undefined);
      } else {
        await rejectClaim(id, notes[id] || 'Rejected after review');
      }
      setClaims((cs) => cs.filter((c) => c.claimId !== id));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not record the decision.');
    } finally {
      setBusyId(null);
    }
  };

  if (claims.length === 0) return null;

  return (
    <section className="mt-10">
      <h2 className="mb-4 text-2xl font-bold text-ink">Damage claims awaiting review</h2>
      <div className="space-y-4">
        {claims.map((c) => (
          <Card key={c.claimId} className="p-5">
            <div className="flex items-center justify-between gap-3">
              <p className="font-medium text-ink">{formatCurrency(c.amount)} claimed</p>
              <Badge tone="amber">Submitted {new Date(c.createdAt).toLocaleDateString()}</Badge>
            </div>
            <p className="mt-2 text-sm text-muted"><span className="font-medium text-ink">Host:</span> {c.description}</p>
            <p className="mt-1 text-sm text-muted">
              <span className="font-medium text-ink">Guest:</span> {c.tenantResponse ?? '(no response yet)'}
            </p>

            {briefs[c.claimId] && (
              <div className="mt-3 rounded-lg bg-gray-50 p-3 text-sm">
                <p className="text-ink">{briefs[c.claimId].brief}</p>
                {briefs[c.claimId].inconsistencies.length > 0 && (
                  <ul className="mt-1 list-disc pl-5 text-muted">
                    {briefs[c.claimId].inconsistencies.map((i) => <li key={i}>{i}</li>)}
                  </ul>
                )}
                <p className="mt-1 text-xs text-muted">{briefs[c.claimId].disclaimer}</p>
              </div>
            )}

            <div className="mt-3 flex flex-wrap items-center gap-2">
              <input type="number" min="0" step="0.01" value={amounts[c.claimId] ?? ''}
                onChange={(e) => setAmounts((a) => ({ ...a, [c.claimId]: e.target.value }))}
                placeholder={`Amount (default ${c.amount})`}
                className="w-40 rounded-lg border border-gray-300 px-3 py-1.5 text-sm" />
              <input value={notes[c.claimId] ?? ''}
                onChange={(e) => setNotes((n) => ({ ...n, [c.claimId]: e.target.value }))}
                placeholder="Decision note (both parties see this)"
                className="min-w-0 flex-1 rounded-lg border border-gray-300 px-3 py-1.5 text-sm" />
              <Button size="sm" disabled={busyId === c.claimId} onClick={() => { void decide(c.claimId, true); }}>Approve</Button>
              <Button size="sm" variant="ghost" disabled={busyId === c.claimId} onClick={() => { void decide(c.claimId, false); }}>Reject</Button>
              {!briefs[c.claimId] && (
                <Button size="sm" variant="ghost" disabled={busyId === c.claimId} onClick={() => { void loadBrief(c.claimId); }}>
                  AI brief
                </Button>
              )}
            </div>
          </Card>
        ))}
      </div>
      {error && <p className="mt-2 text-sm text-red-600">{error}</p>}
    </section>
  );
}
