import { useEffect, useState } from 'react';
import Card from '../ui/Card';
import Button from '../ui/Button';
import Badge from '../ui/Badge';
import { getAuthoredTour, saveAuthoredTour } from '../../api/tours';
import type { TourHotspotDto, TourRoomDto } from '../../api/backend';
import { ApiError } from '../../api/client';
import { getCachedListingPhotos } from '../../lib/listingPhotos';

interface TourEditorModalProps {
  listingId: string;
  listingTitle: string;
  onClose: () => void;
}

const AREAS = ['Entrance', 'Indoor', 'Outdoor', 'Exterior'] as const;
const HOTSPOT_CATEGORIES = [
  'amenity', 'bed', 'seating', 'kitchen', 'bathroom', 'storage',
  'entertainment', 'view', 'outdoor', 'workspace', 'parking',
] as const;

const INPUT =
  'w-full rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm text-ink outline-none focus:border-brand';

let seq = 0;
const newId = (prefix: string) => `${prefix}-${Date.now().toString(36)}-${++seq}`;

function emptyRoom(index: number): TourRoomDto {
  return {
    id: newId('room'),
    name: `Room ${index + 1}`,
    area: 'Indoor',
    caption: '',
    dimensions: null,
    media: null,
    hotspots: [],
  };
}

/**
 * Landlord editor for a listing's guided virtual tour: name and caption the
 * rooms, then click on each room's photo to pin labelled hotspots. Saved
 * through PUT /api/properties/{id}/tour; tenants see it on the listing page.
 */
export default function TourEditorModal({ listingId, listingTitle, onClose }: TourEditorModalProps) {
  const photos = getCachedListingPhotos(listingId) ?? [];
  const [title, setTitle] = useState(listingTitle);
  const [rooms, setRooms] = useState<TourRoomDto[]>([]);
  const [selected, setSelected] = useState(0);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);

  useEffect(() => {
    let cancelled = false;
    getAuthoredTour(listingId)
      .then((tour) => {
        if (cancelled) return;
        if (tour && tour.rooms.length > 0) {
          setTitle(tour.title);
          setRooms(tour.rooms);
        } else {
          // Seed one room per photo this browser holds (or a single blank room).
          const seeded = (photos.length > 0 ? photos : [undefined]).map((_, i) => emptyRoom(i));
          setRooms(seeded);
        }
      })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [listingId]);

  const room = rooms[selected];

  const patchRoom = (patch: Partial<TourRoomDto>) =>
    setRooms((rs) => rs.map((r, i) => (i === selected ? { ...r, ...patch } : r)));

  const addRoom = () => {
    setRooms((rs) => [...rs, emptyRoom(rs.length)]);
    setSelected(rooms.length);
  };

  const removeRoom = () => {
    if (rooms.length <= 1) return;
    setRooms((rs) => rs.filter((_, i) => i !== selected));
    setSelected((i) => Math.max(0, i - 1));
  };

  /** Click on the photo pins a hotspot at that spot (percent coordinates). */
  const placeHotspot = (e: React.MouseEvent<HTMLDivElement>) => {
    const rect = e.currentTarget.getBoundingClientRect();
    const x = Math.round(((e.clientX - rect.left) / rect.width) * 100);
    const y = Math.round(((e.clientY - rect.top) / rect.height) * 100);
    const hotspot: TourHotspotDto = { id: newId('spot'), x, y, label: 'New hotspot', category: 'amenity', detail: '' };
    patchRoom({ hotspots: [...(room?.hotspots ?? []), hotspot] });
  };

  const patchHotspot = (id: string, patch: Partial<TourHotspotDto>) =>
    patchRoom({ hotspots: (room?.hotspots ?? []).map((h) => (h.id === id ? { ...h, ...patch } : h)) });

  const removeHotspot = (id: string) =>
    patchRoom({ hotspots: (room?.hotspots ?? []).filter((h) => h.id !== id) });

  const save = async () => {
    if (saving) return;
    setSaving(true);
    setMessage(null);
    try {
      await saveAuthoredTour(listingId, { title: title.trim() || listingTitle, rooms });
      setMessage({ ok: true, text: 'Tour saved — guests now see these rooms on the listing.' });
    } catch (err) {
      setMessage({ ok: false, text: err instanceof ApiError ? err.message : 'Could not save the tour.' });
    } finally {
      setSaving(false);
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/40 p-4 sm:items-center"
      role="dialog"
      aria-modal="true"
      aria-label={`Virtual tour editor for ${listingTitle}`}
    >
      <Card className="my-8 w-full max-w-3xl p-6">
        <div className="mb-4 flex items-start justify-between gap-4">
          <div>
            <h2 className="text-xl font-bold text-ink">Virtual tour</h2>
            <p className="mt-0.5 text-sm text-muted">
              Name your rooms and click a photo to pin hotspots guests can explore.
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg px-2 py-1 text-xl leading-none text-muted transition-colors hover:bg-gray-100 hover:text-ink"
            aria-label="Close"
          >
            ×
          </button>
        </div>

        {loading ? (
          <p className="py-10 text-center text-sm text-muted">Loading tour…</p>
        ) : (
          <div className="space-y-4">
            <label className="block">
              <span className="mb-1.5 block text-sm font-medium text-ink">Tour title</span>
              <input value={title} onChange={(e) => setTitle(e.target.value)} className={INPUT} />
            </label>

            {/* Room tabs */}
            <div className="flex flex-wrap items-center gap-2">
              {rooms.map((r, i) => (
                <button
                  key={r.id}
                  type="button"
                  onClick={() => setSelected(i)}
                  className={`rounded-full border px-3.5 py-1.5 text-sm font-medium transition-colors ${
                    i === selected ? 'border-brand bg-brand-50 text-brand' : 'border-gray-200 text-gray-600 hover:bg-gray-100'
                  }`}
                >
                  {r.name || `Room ${i + 1}`}
                </button>
              ))}
              <Button variant="ghost" size="sm" onClick={addRoom}>+ Add room</Button>
            </div>

            {room && (
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div className="space-y-3">
                  <label className="block">
                    <span className="mb-1.5 block text-sm text-muted">Room name</span>
                    <input value={room.name} onChange={(e) => patchRoom({ name: e.target.value })} className={INPUT} />
                  </label>
                  <label className="block">
                    <span className="mb-1.5 block text-sm text-muted">Area</span>
                    <select value={room.area} onChange={(e) => patchRoom({ area: e.target.value })} className={INPUT}>
                      {AREAS.map((a) => <option key={a}>{a}</option>)}
                    </select>
                  </label>
                  <label className="block">
                    <span className="mb-1.5 block text-sm text-muted">Caption</span>
                    <input
                      value={room.caption}
                      onChange={(e) => patchRoom({ caption: e.target.value })}
                      placeholder="Bright corner with garden views"
                      className={INPUT}
                    />
                  </label>
                  <label className="block">
                    <span className="mb-1.5 block text-sm text-muted">Dimensions (optional)</span>
                    <input
                      value={room.dimensions ?? ''}
                      onChange={(e) => patchRoom({ dimensions: e.target.value || null })}
                      placeholder="4.2m × 3.8m"
                      className={INPUT}
                    />
                  </label>
                  {rooms.length > 1 && (
                    <Button variant="ghost" size="sm" className="text-rose-600 hover:bg-rose-50" onClick={removeRoom}>
                      Remove this room
                    </Button>
                  )}
                </div>

                {/* Photo canvas with pinned hotspots */}
                <div>
                  <span className="mb-1.5 block text-sm text-muted">
                    Click the photo to pin a hotspot ({room.hotspots.length} pinned)
                  </span>
                  <div
                    onClick={placeHotspot}
                    className="relative aspect-[4/3] w-full cursor-crosshair overflow-hidden rounded-xl border border-gray-200 bg-gradient-to-br from-gray-100 to-gray-200"
                  >
                    {photos[selected] && (
                      <img src={photos[selected]} alt={room.name} className="h-full w-full object-cover" />
                    )}
                    {room.hotspots.map((h) => (
                      <span
                        key={h.id}
                        title={h.label}
                        className="absolute flex h-5 w-5 -translate-x-1/2 -translate-y-1/2 items-center justify-center rounded-full bg-brand text-[10px] font-bold text-white ring-2 ring-white"
                        style={{ left: `${h.x}%`, top: `${h.y}%` }}
                      >
                        {room.hotspots.indexOf(h) + 1}
                      </span>
                    ))}
                    {!photos[selected] && (
                      <span className="absolute inset-0 flex items-center justify-center px-6 text-center text-xs text-muted">
                        No photo cached in this browser for this room — hotspots still save and show on the tour's scene.
                      </span>
                    )}
                  </div>

                  {room.hotspots.length > 0 && (
                    <ul className="mt-3 space-y-2">
                      {room.hotspots.map((h, i) => (
                        <li key={h.id} className="flex items-center gap-2">
                          <Badge tone="green">{i + 1}</Badge>
                          <input
                            value={h.label}
                            onChange={(e) => patchHotspot(h.id, { label: e.target.value })}
                            className={`${INPUT} flex-1`}
                            aria-label={`Hotspot ${i + 1} label`}
                          />
                          <select
                            value={h.category}
                            onChange={(e) => patchHotspot(h.id, { category: e.target.value })}
                            className="rounded-lg border border-gray-200 bg-white px-2 py-2 text-sm outline-none focus:border-brand"
                            aria-label={`Hotspot ${i + 1} category`}
                          >
                            {HOTSPOT_CATEGORIES.map((c) => <option key={c}>{c}</option>)}
                          </select>
                          <button
                            type="button"
                            onClick={() => removeHotspot(h.id)}
                            aria-label={`Remove hotspot ${i + 1}`}
                            className="px-1 text-lg leading-none text-muted hover:text-rose-600"
                          >
                            ×
                          </button>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>
              </div>
            )}

            {message && (
              <p className={`text-sm ${message.ok ? 'text-brand' : 'text-rose-600'}`} role={message.ok ? undefined : 'alert'}>
                {message.text}
              </p>
            )}

            <div className="flex justify-end gap-2 border-t border-gray-100 pt-4">
              <Button variant="ghost" size="sm" onClick={onClose}>Close</Button>
              <Button size="sm" onClick={() => void save()} disabled={saving}>
                {saving ? 'Saving…' : 'Save tour'}
              </Button>
            </div>
          </div>
        )}
      </Card>
    </div>
  );
}
