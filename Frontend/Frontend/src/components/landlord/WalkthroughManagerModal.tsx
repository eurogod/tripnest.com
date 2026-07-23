import { useEffect, useRef, useState } from 'react';
import Card from '../ui/Card';
import Button from '../ui/Button';
import Badge from '../ui/Badge';
import { getCachedListingPhotos, cachedPhotoFiles } from '../../lib/listingPhotos';
import { getClipsForProperty, type StoredClip } from '../../lib/clipStore';
import {
  generateWalkthroughClips, submitExistingClip, isVeoConfigured, MAX_CLIPS_PER_LISTING,
} from '../../lib/walkthroughGenerator';
import { getWalkthroughs } from '../../api/walkthroughs';
import { useGeneration, type SlotState } from '../../store/walkthroughStore';

interface WalkthroughManagerModalProps {
  listingId: string;
  listingTitle: string;
  onClose: () => void;
}

type SlotDisplay =
  | { kind: 'queued' | 'generating' | 'failed'; slot: SlotState }
  | { kind: 'ready'; clip: StoredClip; slot?: SlotState }
  | { kind: 'photo-only' };

/**
 * Landlord-side control room for a listing's AI video walkthrough: one slot
 * per uploaded photo showing generation state, with generate/retry/
 * regenerate/preview actions and the backend review-submission count.
 */
export default function WalkthroughManagerModal({
  listingId, listingTitle, onClose,
}: WalkthroughManagerModalProps) {
  const photos = getCachedListingPhotos(listingId)?.slice(0, MAX_CLIPS_PER_LISTING) ?? [];
  const gen = useGeneration(listingId);
  const [clips, setClips] = useState<Map<number, StoredClip>>(new Map());
  const [submittedCount, setSubmittedCount] = useState<number | null>(null);
  // Modal-owned preview object URL: created on click, replaced per selection,
  // revoked on swap/unmount.
  const [preview, setPreviewState] = useState<{ index: number; url: string } | null>(null);
  const previewRef = useRef<{ index: number; url: string } | null>(null);
  const setPreview = (next: { index: number; url: string } | null) => {
    if (previewRef.current) URL.revokeObjectURL(previewRef.current.url);
    previewRef.current = next;
    setPreviewState(next);
  };
  const togglePreview = (i: number) => {
    if (preview?.index === i) {
      setPreview(null);
      return;
    }
    const blob = clips.get(i)?.blob;
    setPreview(blob ? { index: i, url: URL.createObjectURL(blob) } : null);
  };
  useEffect(() => () => {
    if (previewRef.current) URL.revokeObjectURL(previewRef.current.url);
  }, []);

  // Reload durable state whenever the live run changes (slots flip to ready
  // as clips land) and when the modal opens.
  useEffect(() => {
    let cancelled = false;
    getClipsForProperty(listingId)
      .then((m) => { if (!cancelled) setClips(m); })
      .catch(() => {});
    getWalkthroughs(listingId)
      .then((list) => { if (!cancelled) setSubmittedCount(list.length); })
      .catch(() => { if (!cancelled) setSubmittedCount(null); });
    return () => { cancelled = true; };
  }, [listingId, gen]);

  const slotFor = (i: number): SlotDisplay => {
    const live = gen?.slots[i];
    if (live && (live.status === 'queued' || live.status === 'generating' || live.status === 'failed')) {
      return { kind: live.status, slot: live };
    }
    const clip = clips.get(i);
    if (clip) return { kind: 'ready', clip, slot: live };
    return { kind: 'photo-only' };
  };

  const running = gen?.running ?? false;
  const veoReady = isVeoConfigured();
  const hasPhotoOnly = photos.some((_, i) => slotFor(i).kind === 'photo-only');

  const generate = async (indexes?: number[], force = false) => {
    const files = await cachedPhotoFiles(listingId);
    void generateWalkthroughClips(listingId, files, { indexes, force });
  };

  const submitBackfill = (i: number) => {
    const clip = clips.get(i);
    if (clip) void submitExistingClip(listingId, i, clip.blob);
  };

  const BADGE: Record<string, { tone: 'gray' | 'blue' | 'amber' | 'green' | 'red'; label: string }> = {
    'photo-only': { tone: 'gray', label: 'Photo only' },
    queued: { tone: 'blue', label: 'Queued' },
    generating: { tone: 'amber', label: 'Generating…' },
    ready: { tone: 'green', label: 'Video ready' },
    failed: { tone: 'red', label: 'Failed' },
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/40 p-4 sm:items-center"
      role="dialog"
      aria-modal="true"
      aria-label={`Video walkthrough for ${listingTitle}`}
    >
      <Card className="my-8 w-full max-w-xl p-6">
        <div className="mb-4 flex items-start justify-between gap-4">
          <div>
            <h2 className="text-xl font-bold text-ink">Video walkthrough</h2>
            <p className="mt-0.5 text-sm text-muted">{listingTitle}</p>
            <p className="mt-1 text-xs text-muted">
              {submittedCount === null
                ? 'Review status unavailable'
                : submittedCount === 0
                  ? 'Nothing submitted for review yet'
                  : `${submittedCount} video${submittedCount === 1 ? '' : 's'} submitted for review`}
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

        {photos.length === 0 ? (
          <p className="rounded-lg bg-gray-50 px-4 py-6 text-center text-sm text-muted">
            No photos available in this browser. Walkthrough videos are generated
            from listing photos — add photos when creating a listing to enable them.
          </p>
        ) : (
          <div className="space-y-3">
            {photos.map((photo, i) => {
              const display = slotFor(i);
              const badge = BADGE[display.kind];
              return (
                <div key={i} className="rounded-xl border border-gray-100 p-3">
                  <div className="flex items-center gap-3">
                    <img src={photo} alt={`Listing photo ${i + 1}`} className="h-14 w-14 shrink-0 rounded-lg object-cover" />
                    <div className="min-w-0 flex-1">
                      <p className="text-sm font-medium text-ink">Room {i + 1}</p>
                      <div className="mt-1 flex flex-wrap items-center gap-1.5">
                        <Badge tone={badge.tone}>{badge.label}</Badge>
                        {display.kind === 'ready' && display.slot?.uploaded && (
                          <Badge tone="green">Submitted</Badge>
                        )}
                        {display.kind === 'ready' && display.slot?.uploadError && (
                          <Badge tone="red">Submit failed</Badge>
                        )}
                      </div>
                      {display.kind === 'failed' && display.slot.error && (
                        <p className="mt-1 truncate text-xs text-rose-600">{display.slot.error}</p>
                      )}
                    </div>
                    <div className="flex shrink-0 gap-1.5">
                      {display.kind === 'ready' && (
                        <>
                          <Button variant="ghost" size="sm" onClick={() => togglePreview(i)}>
                            {preview?.index === i ? 'Hide' : 'Preview'}
                          </Button>
                          <Button variant="ghost" size="sm" disabled={running} onClick={() => generate([i], true)}>
                            Regenerate
                          </Button>
                          {!display.slot?.uploaded && (
                            <Button variant="ghost" size="sm" disabled={running} onClick={() => submitBackfill(i)}>
                              Submit
                            </Button>
                          )}
                        </>
                      )}
                      {display.kind === 'failed' && (
                        <Button variant="ghost" size="sm" disabled={running} onClick={() => generate([i])}>
                          Retry
                        </Button>
                      )}
                    </div>
                  </div>
                  {preview?.index === i && (
                    <video src={preview.url} controls playsInline className="mt-3 max-h-72 w-full rounded-lg bg-black" />
                  )}
                </div>
              );
            })}
          </div>
        )}

        <div className="mt-5 flex flex-wrap items-center justify-between gap-3 border-t border-gray-100 pt-4">
          {veoReady ? (
            <p className="text-xs text-muted">
              Videos use Google Flow (Veo) AI when available, with free
              cinematic photo motion as fallback.
            </p>
          ) : (
            <p className="text-xs text-muted">
              Videos use free cinematic photo motion. Add a billed{' '}
              <code className="rounded bg-gray-100 px-1">VITE_GEMINI_API_KEY</code> for
              AI-generated walkthroughs.
            </p>
          )}
          <div className="flex gap-2">
            {photos.length > 0 && hasPhotoOnly && (
              <Button size="sm" disabled={running} onClick={() => generate()}>
                {running ? 'Generating…' : 'Generate videos'}
              </Button>
            )}
            <Button variant="ghost" size="sm" onClick={onClose}>Close</Button>
          </div>
        </div>
      </Card>
    </div>
  );
}
