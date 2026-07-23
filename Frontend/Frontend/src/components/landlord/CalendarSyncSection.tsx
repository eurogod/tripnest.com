import { useEffect, useState } from 'react';
import {
  addExternalCalendar, getExternalCalendars, getIcalFeedUrl, removeExternalCalendar,
  syncExternalCalendar, type ExternalCalendarDto,
} from '../../api/calendar';
import { getListings } from '../../api/listings';
import type { Listing } from '../../types';
import Card from '../ui/Card';
import Button from '../ui/Button';

/**
 * Two-way iCal sync per property: export our availability as a tokenized .ics URL (paste into
 * Airbnb/Booking/Google Calendar), and import their feeds so externally-booked dates block here.
 */
export default function CalendarSyncSection() {
  const [listings, setListings] = useState<Listing[]>([]);
  const [propertyId, setPropertyId] = useState('');
  // Keyed by the property it answered for — a key mismatch reads as "not loaded
  // yet", so switching properties needs no synchronous reset inside the effect.
  const [feedResult, setFeedResult] = useState<{ key: string; url: string | null }>({ key: '', url: null });
  const feedUrl = feedResult.key === propertyId ? feedResult.url : null;
  const [feeds, setFeeds] = useState<ExternalCalendarDto[]>([]);
  const [name, setName] = useState('');
  const [url, setUrl] = useState('');
  const [busy, setBusy] = useState(false);
  const [note, setNote] = useState<string | null>(null);

  useEffect(() => {
    getListings().then((ls) => {
      setListings(ls);
      if (ls.length > 0) setPropertyId(ls[0].id);
    }).catch(() => setListings([]));
  }, []);

  useEffect(() => {
    if (!propertyId) return;
    getIcalFeedUrl(propertyId)
      .then((url) => setFeedResult({ key: propertyId, url }))
      .catch(() => setFeedResult({ key: propertyId, url: null }));
    getExternalCalendars(propertyId).then(setFeeds).catch(() => setFeeds([]));
  }, [propertyId]);

  const add = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setNote(null);
    try {
      const created = await addExternalCalendar(propertyId, name.trim(), url.trim());
      setFeeds((fs) => [...fs, created]);
      setName('');
      setUrl('');
      setNote('Feed added — it syncs now and then on a schedule.');
    } catch (err) {
      setNote(err instanceof Error ? err.message : 'Could not add the feed.');
    } finally {
      setBusy(false);
    }
  };

  const sync = async (id: string) => {
    setBusy(true);
    try {
      const updated = await syncExternalCalendar(id);
      setFeeds((fs) => fs.map((f) => (f.id === id ? updated : f)));
    } catch (err) {
      setNote(err instanceof Error ? err.message : 'Sync failed.');
    } finally {
      setBusy(false);
    }
  };

  const remove = async (id: string) => {
    setBusy(true);
    try {
      await removeExternalCalendar(id);
      setFeeds((fs) => fs.filter((f) => f.id !== id));
    } catch (err) {
      setNote(err instanceof Error ? err.message : 'Could not remove the feed.');
    } finally {
      setBusy(false);
    }
  };

  if (listings.length === 0) return null;

  return (
    <Card className="mt-8 p-5">
      <h2 className="text-base font-bold text-ink">Calendar sync (iCal)</h2>
      <p className="mt-1 text-sm text-muted">
        Keep availability in step with other platforms — export this calendar, import theirs.
      </p>

      <select
        value={propertyId}
        onChange={(e) => setPropertyId(e.target.value)}
        aria-label="Property"
        className="mt-3 rounded-lg border border-gray-300 px-3 py-2 text-sm"
      >
        {listings.map((l) => <option key={l.id} value={l.id}>{l.title}</option>)}
      </select>

      {feedUrl && (
        <div className="mt-3 rounded-lg bg-gray-50 p-3">
          <p className="text-xs font-medium text-ink">Export — paste this URL into Airbnb / Booking / Google Calendar:</p>
          <div className="mt-1 flex items-center gap-2">
            <code className="min-w-0 flex-1 truncate text-xs text-muted">{feedUrl}</code>
            <Button size="sm" variant="ghost"
              onClick={() => { void navigator.clipboard.writeText(feedUrl).then(() => setNote('Feed URL copied.')); }}>
              Copy
            </Button>
          </div>
        </div>
      )}

      <div className="mt-4">
        <p className="text-xs font-medium text-ink">Import — feeds whose bookings should block dates here:</p>
        {feeds.length > 0 && (
          <div className="mt-2 space-y-2">
            {feeds.map((f) => (
              <div key={f.id} className="flex items-center justify-between gap-3 rounded-lg bg-gray-50 px-3 py-2">
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium text-ink">{f.name}</p>
                  <p className="truncate text-xs text-muted">
                    {f.lastSyncError
                      ? `Last sync failed: ${f.lastSyncError}`
                      : f.lastSyncedAt
                        ? `${f.importedRanges} blocked ranges · synced ${new Date(f.lastSyncedAt).toLocaleString()}`
                        : 'Not synced yet'}
                  </p>
                </div>
                <div className="flex shrink-0 gap-1">
                  <Button size="sm" variant="ghost" disabled={busy} onClick={() => { void sync(f.id); }}>Sync now</Button>
                  <Button size="sm" variant="ghost" disabled={busy} onClick={() => { void remove(f.id); }}>Remove</Button>
                </div>
              </div>
            ))}
          </div>
        )}
        <form onSubmit={add} className="mt-2 flex flex-col gap-2 sm:flex-row">
          <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Name (e.g. Airbnb)"
            required className="rounded-lg border border-gray-300 px-3 py-2 text-sm sm:w-40" />
          <input value={url} onChange={(e) => setUrl(e.target.value)} placeholder="https://…/calendar.ics"
            required type="url" className="flex-1 rounded-lg border border-gray-300 px-3 py-2 text-sm" />
          <Button size="sm" disabled={busy}>{busy ? 'Adding…' : 'Add feed'}</Button>
        </form>
      </div>

      {note && <p className="mt-2 text-sm text-muted">{note}</p>}
    </Card>
  );
}
