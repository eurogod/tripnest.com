import { useEffect, useState } from 'react';
import { getBookingClaims, respondToClaim, CLAIM_STATUS, type DamageClaimDto } from '../../api/claims';
import { apiGetList } from '../../api/client';
import type { BookingResponseDto } from '../../api/backend';
import Card from '../ui/Card';
import Button from '../ui/Button';
import Badge, { type BadgeTone } from '../ui/Badge';
import { formatCedi } from '../../lib/format';

const TONE: Record<string, BadgeTone> = { Submitted: 'amber', Approved: 'red', Rejected: 'green' };

/**
 * Damage claims filed against the tenant's stays. While a claim is still Submitted the tenant
 * can add their side of the story once — an admin reads both before deciding.
 */
export default function TenantClaimsSection() {
  const [claims, setClaims] = useState<DamageClaimDto[]>([]);
  const [replyFor, setReplyFor] = useState<string | null>(null);
  const [reply, setReply] = useState('');
  const [busy, setBusy] = useState(false);
  const [note, setNote] = useState<string | null>(null);

  useEffect(() => {
    (async () => {
      const bookings = await apiGetList<BookingResponseDto>('/api/bookings/user/my-bookings').catch(() => []);
      const per = await Promise.all(bookings.map((b) => getBookingClaims(b.bookingId).catch(() => [])));
      setClaims(per.flat());
    })();
  }, []);

  const send = async (claimId: string) => {
    setBusy(true);
    setNote(null);
    try {
      const updated = await respondToClaim(claimId, reply.trim());
      setClaims((cs) => cs.map((c) => (c.claimId === claimId ? updated : c)));
      setReplyFor(null);
      setReply('');
      setNote('Response recorded — an admin will review both sides.');
    } catch (e) {
      setNote(e instanceof Error ? e.message : 'Could not send the response.');
    } finally {
      setBusy(false);
    }
  };

  if (claims.length === 0) return null;

  return (
    <section className="mt-8">
      <Card className="p-5">
        <h2 className="text-base font-bold text-ink">Damage claims on your stays</h2>
        <div className="mt-3 space-y-3">
          {claims.map((c) => {
            const st = CLAIM_STATUS[c.status] ?? 'Submitted';
            return (
              <div key={c.claimId} className="rounded-lg bg-gray-50 p-3">
                <div className="flex items-center justify-between gap-3">
                  <p className="text-sm font-medium text-ink">{formatCedi(c.amount)} claimed by the host</p>
                  <Badge tone={TONE[st]}>{st === 'Approved' ? 'Upheld' : st === 'Rejected' ? 'Dismissed' : 'Awaiting review'}</Badge>
                </div>
                <p className="mt-1 text-sm text-muted">{c.description}</p>
                {c.tenantResponse ? (
                  <p className="mt-1 text-sm text-muted"><span className="font-medium text-ink">Your response:</span> {c.tenantResponse}</p>
                ) : st === 'Submitted' && (
                  replyFor === c.claimId ? (
                    <div className="mt-2 space-y-2">
                      <textarea value={reply} onChange={(e) => setReply(e.target.value)} rows={3}
                        placeholder="Your side of the story…" required
                        className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm" />
                      <div className="flex gap-2">
                        <Button size="sm" disabled={busy || !reply.trim()} onClick={() => { void send(c.claimId); }}>
                          {busy ? 'Sending…' : 'Send response'}
                        </Button>
                        <Button size="sm" variant="ghost" onClick={() => setReplyFor(null)}>Cancel</Button>
                      </div>
                    </div>
                  ) : (
                    <Button size="sm" variant="ghost" className="mt-2" onClick={() => setReplyFor(c.claimId)}>
                      Respond
                    </Button>
                  )
                )}
                {c.resolutionNote && (
                  <p className="mt-1 text-sm text-muted">
                    <span className="font-medium text-ink">Decision:</span> {c.resolutionNote}
                    {c.approvedAmount != null && ` (${formatCedi(c.approvedAmount)})`}
                  </p>
                )}
              </div>
            );
          })}
        </div>
        {note && <p className="mt-2 text-sm text-muted">{note}</p>}
      </Card>
    </section>
  );
}
