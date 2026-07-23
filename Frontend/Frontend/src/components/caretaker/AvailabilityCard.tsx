import { useEffect, useState } from 'react';
import { getMyCaretakerProfile, setMyAvailability } from '../../api/caretakerWorkspace';
import Card from '../ui/Card';
import Button from '../ui/Button';
import Badge from '../ui/Badge';

/**
 * The caretaker's availability switch: Active takes new service requests, Inactive pauses them
 * (existing assignments are unaffected). Suspended is admin-set and can't be self-changed.
 */
export default function AvailabilityCard() {
  const [status, setStatus] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [note, setNote] = useState<string | null>(null);

  useEffect(() => {
    getMyCaretakerProfile().then((p) => setStatus(p.status)).catch(() => setStatus(null));
  }, []);

  if (status === null || status === 2) return null; // no profile yet, or suspended (admin-controlled)

  const active = status === 0;

  const toggle = async () => {
    setBusy(true);
    setNote(null);
    try {
      await setMyAvailability(!active);
      setStatus(active ? 1 : 0);
    } catch (e) {
      setNote(e instanceof Error ? e.message : 'Could not update availability.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card className="flex items-center justify-between gap-4 p-5">
      <div>
        <div className="flex items-center gap-2">
          <h2 className="text-base font-bold text-ink">Availability</h2>
          <Badge tone={active ? 'green' : 'gray'}>{active ? 'Taking new work' : 'Paused'}</Badge>
        </div>
        <p className="mt-1 text-sm text-muted">
          {active
            ? 'Landlords can send you new service requests.'
            : 'New requests are paused; your current assignments continue.'}
        </p>
        {note && <p className="mt-1 text-sm text-red-600">{note}</p>}
      </div>
      <Button size="sm" variant={active ? 'ghost' : undefined} disabled={busy} onClick={() => { void toggle(); }}>
        {busy ? 'Saving…' : active ? 'Pause new work' : 'Go available'}
      </Button>
    </Card>
  );
}
