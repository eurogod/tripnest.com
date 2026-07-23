import { useEffect, useState } from 'react';
import { fileClaim, getMyClaims, CLAIM_STATUS, type DamageClaimDto } from '../../api/claims';
import { getLandlordBookings } from '../../api/landlord';
import type { LandlordBooking } from '../../types';
import Card from '../ui/Card';
import Button from '../ui/Button';
import Badge, { type BadgeTone } from '../ui/Badge';
import { formatCedi } from '../../lib/format';

const TONE: Record<string, BadgeTone> = { Submitted: 'amber', Approved: 'green', Rejected: 'red' };

/**
 * Damage claims the landlord has filed, plus the form to file a new one against a completed
 * stay (photos as evidence). The tenant gets to respond; an admin decides and the payout —
 * full or reduced — follows the decision.
 */
export default function ClaimsSection() {
  const [claims, setClaims] = useState<DamageClaimDto[]>([]);
  const [bookings, setBookings] = useState<LandlordBooking[]>([]);
  const [formOpen, setFormOpen] = useState(false);
  const [bookingId, setBookingId] = useState('');
  const [amount, setAmount] = useState('');
  const [description, setDescription] = useState('');
  const [photos, setPhotos] = useState<File[]>([]);
  const [busy, setBusy] = useState(false);
  const [note, setNote] = useState<string | null>(null);

  useEffect(() => {
    getMyClaims().then(setClaims).catch(() => setClaims([]));
    getLandlordBookings().then((bs) => {
      const completed = bs.filter((b) => b.status === 'completed');
      setBookings(completed);
      if (completed.length > 0) setBookingId(completed[0].id);
    }).catch(() => setBookings([]));
  }, []);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setNote(null);
    try {
      const created = await fileClaim(bookingId, Number(amount), description.trim(), photos);
      setClaims((cs) => [created, ...cs]);
      setFormOpen(false);
      setAmount('');
      setDescription('');
      setPhotos([]);
      setNote('Claim filed — the guest can respond before an admin reviews it.');
    } catch (err) {
      setNote(err instanceof Error ? err.message : 'Could not file the claim.');
    } finally {
      setBusy(false);
    }
  };

  if (claims.length === 0 && bookings.length === 0) return null;

  return (
    <section className="mt-10">
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-2xl font-bold text-ink">Damage claims</h2>
        {bookings.length > 0 && !formOpen && (
          <Button size="sm" variant="ghost" onClick={() => setFormOpen(true)}>File a claim</Button>
        )}
      </div>

      {formOpen && (
        <Card className="mb-4 p-5">
          <form onSubmit={submit} className="space-y-2">
            <select value={bookingId} onChange={(e) => setBookingId(e.target.value)} aria-label="Completed stay"
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm">
              {bookings.map((b) => (
                <option key={b.id} value={b.id}>{b.listing} — {b.guest} ({b.checkIn} → {b.checkOut})</option>
              ))}
            </select>
            <input type="number" min="1" step="0.01" value={amount} onChange={(e) => setAmount(e.target.value)}
              placeholder="Amount claimed (GH₵)" required
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm" />
            <textarea value={description} onChange={(e) => setDescription(e.target.value)}
              placeholder="What was damaged, and how?" required rows={3}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm" />
            <input type="file" accept="image/*" multiple
              onChange={(e) => setPhotos(Array.from(e.target.files ?? []))}
              aria-label="Evidence photos" className="block w-full text-sm" />
            <div className="flex gap-2">
              <Button size="sm" disabled={busy}>{busy ? 'Filing…' : 'File claim'}</Button>
              <Button size="sm" variant="ghost" onClick={() => setFormOpen(false)}>Cancel</Button>
            </div>
          </form>
        </Card>
      )}

      {claims.length > 0 && (
        <div className="space-y-3">
          {claims.map((c) => {
            const st = CLAIM_STATUS[c.status] ?? 'Submitted';
            return (
              <Card key={c.claimId} className="p-4">
                <div className="flex items-center justify-between gap-3">
                  <p className="text-sm font-medium text-ink">{formatCedi(c.amount)} claimed</p>
                  <Badge tone={TONE[st]}>{st}</Badge>
                </div>
                <p className="mt-1 text-sm text-muted">{c.description}</p>
                {c.tenantResponse && (
                  <p className="mt-1 text-sm text-muted"><span className="font-medium text-ink">Guest:</span> {c.tenantResponse}</p>
                )}
                {c.resolutionNote && (
                  <p className="mt-1 text-sm text-muted">
                    <span className="font-medium text-ink">Decision:</span> {c.resolutionNote}
                    {c.approvedAmount != null && ` (${formatCedi(c.approvedAmount)} approved)`}
                  </p>
                )}
              </Card>
            );
          })}
        </div>
      )}

      {note && <p className="mt-2 text-sm text-muted">{note}</p>}
    </section>
  );
}
