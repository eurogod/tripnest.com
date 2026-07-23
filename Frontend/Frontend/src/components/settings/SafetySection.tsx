import { useEffect, useState } from 'react';
import { getTrustedContact, requestUrgentHelp, saveTrustedContact } from '../../api/safety';
import Card from '../ui/Card';
import Button from '../ui/Button';

/**
 * Safety: the trusted contact we notify on stay check-ins, and the urgent-help line — a
 * queue-jumping ticket that pages every admin and returns the 24/7 hotline number.
 */
export default function SafetySection() {
  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [email, setEmail] = useState('');
  const [saved, setSaved] = useState(false);
  const [busy, setBusy] = useState(false);
  const [note, setNote] = useState<string | null>(null);

  const [urgentOpen, setUrgentOpen] = useState(false);
  const [urgentMsg, setUrgentMsg] = useState('');
  const [urgentBusy, setUrgentBusy] = useState(false);
  const [urgentResult, setUrgentResult] = useState<string | null>(null);

  useEffect(() => {
    getTrustedContact()
      .then((c) => {
        setName(c.name ?? '');
        setPhone(c.phone ?? '');
        setEmail(c.email ?? '');
      })
      .catch(() => { /* none saved yet */ });
  }, []);

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setNote(null);
    setSaved(false);
    try {
      await saveTrustedContact({ name: name.trim(), phone: phone.trim(), email: email.trim() || undefined });
      setSaved(true);
    } catch (err) {
      setNote(err instanceof Error ? err.message : 'Could not save the contact.');
    } finally {
      setBusy(false);
    }
  };

  const sendUrgent = async (e: React.FormEvent) => {
    e.preventDefault();
    setUrgentBusy(true);
    try {
      const r = await requestUrgentHelp(urgentMsg.trim());
      setUrgentResult(
        r.hotline
          ? `Help is on the way — an admin responds within ~${r.promisedResponseMinutes} min. Call the 24/7 hotline now: ${r.hotline}`
          : `Help is on the way — an admin responds within ~${r.promisedResponseMinutes} min.`,
      );
      setUrgentOpen(false);
      setUrgentMsg('');
    } catch (err) {
      setUrgentResult(err instanceof Error ? err.message : 'Could not reach urgent support — try again.');
    } finally {
      setUrgentBusy(false);
    }
  };

  return (
    <Card className="p-6">
      <h2 className="text-base font-bold text-ink">Safety</h2>
      <p className="mt-1 text-sm text-muted">
        Your trusted contact is notified when you check in to a stay.
      </p>

      <form onSubmit={save} className="mt-3 grid gap-2 sm:grid-cols-3">
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="Contact name"
          required
          className="rounded-lg border border-gray-300 px-3 py-2 text-sm"
        />
        <input
          value={phone}
          onChange={(e) => setPhone(e.target.value)}
          placeholder="Phone (e.g. 024 123 4567)"
          required
          inputMode="tel"
          className="rounded-lg border border-gray-300 px-3 py-2 text-sm"
        />
        <input
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="Email (optional)"
          className="rounded-lg border border-gray-300 px-3 py-2 text-sm"
        />
        <div className="sm:col-span-3">
          <Button size="sm" disabled={busy}>{busy ? 'Saving…' : saved ? 'Saved ✓' : 'Save contact'}</Button>
        </div>
      </form>
      {note && <p className="mt-2 text-sm text-red-600">{note}</p>}

      <div className="mt-5 border-t border-gray-100 pt-4">
        <h3 className="text-sm font-semibold text-ink">Need help right now?</h3>
        <p className="mt-1 text-sm text-muted">
          Locked out, or feeling unsafe at a stay? This pages our team immediately, day or night.
        </p>
        {urgentOpen ? (
          <form onSubmit={sendUrgent} className="mt-2 space-y-2">
            <textarea
              value={urgentMsg}
              onChange={(e) => setUrgentMsg(e.target.value)}
              placeholder="Tell us what's happening…"
              required
              rows={3}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm"
            />
            <div className="flex gap-2">
              <Button size="sm" disabled={urgentBusy}>{urgentBusy ? 'Sending…' : 'Get urgent help'}</Button>
              <Button size="sm" variant="ghost" onClick={() => setUrgentOpen(false)}>Cancel</Button>
            </div>
          </form>
        ) : (
          <Button size="sm" variant="ghost" className="mt-2" onClick={() => setUrgentOpen(true)}>
            Get urgent help
          </Button>
        )}
        {urgentResult && <p className="mt-2 text-sm font-medium text-ink">{urgentResult}</p>}
      </div>
    </Card>
  );
}
