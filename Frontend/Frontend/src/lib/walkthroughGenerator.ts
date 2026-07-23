import { generateClipFromPhoto, isVeoConfigured } from './veoClient';
import { renderKenBurnsClip } from './kenBurnsRenderer';
import { hasClip, putClip } from './clipStore';
import { getTokens } from '../api/client';
import { deleteWalkthrough, getWalkthroughs, uploadWalkthrough } from '../api/walkthroughs';
import { beginRun, endRun, isRunning, patchSlot } from '../store/walkthroughStore';

// Turns uploaded listing photos into walkthrough video clips, stores them
// locally, and submits each to the backend review queue. Provider order:
// Veo (Google Flow's model) when a billed key is configured, falling back
// to the free in-browser Ken Burns renderer — so generation always
// produces a clip. Sequential on purpose: Veo calls are paid and
// rate-limited, and the local renderer records in realtime. Progress is
// narrated through walkthroughStore for the landlord manager.

export const MAX_CLIPS_PER_LISTING = 5;

export { isVeoConfigured };

export interface GenerateOptions {
  /** Room indexes to generate; defaults to every photo up to the cap. */
  indexes?: number[];
  /** Regenerate even when a clip already exists (replaces backend copy too). */
  force?: boolean;
}

/** Deterministic per-slot title — doubles as the backend dedup key. */
function slotTitle(roomIndex: number): string {
  return `Room ${roomIndex + 1} walkthrough`;
}

// Local record of successful uploads: roomIndex -> walkthroughId.
function uploadsKey(propertyId: string): string {
  return `tripnest.walkthroughUploads.${propertyId}`;
}

function readUploads(propertyId: string): Record<number, string> {
  try {
    const raw = localStorage.getItem(uploadsKey(propertyId));
    return raw ? (JSON.parse(raw) as Record<number, string>) : {};
  } catch {
    return {};
  }
}

function writeUploads(propertyId: string, uploads: Record<number, string>): void {
  try {
    localStorage.setItem(uploadsKey(propertyId), JSON.stringify(uploads));
  } catch { /* quota — non-fatal */ }
}

/** Submit a clip to the backend review queue; never throws. */
async function submitClip(
  propertyId: string,
  roomIndex: number,
  blob: Blob,
  existingTitles: Map<string, string>,
  force: boolean,
): Promise<void> {
  if (!getTokens()) return; // not signed in — nothing to submit
  const title = slotTitle(roomIndex);
  try {
    const previousId = readUploads(propertyId)[roomIndex] ?? existingTitles.get(title);
    if (!force && previousId) {
      patchSlot(propertyId, roomIndex, { uploaded: true });
      return;
    }
    const created = await uploadWalkthrough(propertyId, title, blob);
    // Replace, don't accumulate: drop the superseded backend copy.
    if (force && previousId) {
      await deleteWalkthrough(propertyId, previousId).catch(() => {});
    }
    writeUploads(propertyId, { ...readUploads(propertyId), [roomIndex]: created.walkthroughId });
    patchSlot(propertyId, roomIndex, { uploaded: true });
  } catch (err) {
    patchSlot(propertyId, roomIndex, {
      uploadError: err instanceof Error ? err.message : 'Upload failed',
    });
  }
}

/**
 * Generate a clip per photo (up to the cap), skipping room indexes that
 * already have one unless forced. Failures leave the room on its photo
 * still with the clip marked pending — the tour degrades gracefully.
 */
export async function generateWalkthroughClips(
  propertyId: string,
  photos: File[],
  opts: GenerateOptions = {},
): Promise<void> {
  if (isRunning(propertyId)) return;

  const capped = photos.slice(0, MAX_CLIPS_PER_LISTING);
  const candidates = opts.indexes ?? capped.map((_, i) => i);

  const targets: number[] = [];
  for (const i of candidates) {
    if (i < 0 || i >= capped.length) continue;
    if (!opts.force && (await hasClip(propertyId, i))) continue;
    targets.push(i);
  }
  if (targets.length === 0) return;

  beginRun(propertyId, targets);
  // Server truth for upload dedup, fetched once per run.
  const existingTitles = new Map<string, string>();
  if (getTokens()) {
    try {
      for (const w of await getWalkthroughs(propertyId)) {
        existingTitles.set(w.title, w.walkthroughId);
      }
    } catch { /* backend down — local record still applies */ }
  }

  try {
    for (const i of targets) {
      patchSlot(propertyId, i, { status: 'generating' });
      try {
        let blob: Blob | undefined;
        let provider: 'google-flow' | 'local' = 'local';
        if (isVeoConfigured()) {
          try {
            blob = await generateClipFromPhoto(capped[i]);
            provider = 'google-flow';
          } catch (err) {
            console.warn(`Veo failed for ${propertyId} room ${i}; using free local render:`, err);
          }
        }
        blob ??= await renderKenBurnsClip(capped[i], i);
        await putClip({
          propertyId,
          roomIndex: i,
          blob,
          sourcePhoto: capped[i].name,
          generatedAt: new Date().toISOString(),
          provider,
        });
        patchSlot(propertyId, i, { status: 'ready' });
        await submitClip(propertyId, i, blob, existingTitles, opts.force ?? false);
      } catch (err) {
        console.warn(`Walkthrough clip generation failed for ${propertyId} room ${i}:`, err);
        patchSlot(propertyId, i, {
          status: 'failed',
          error: err instanceof Error ? err.message : 'Generation failed',
        });
      }
    }
  } finally {
    endRun(propertyId);
  }
}

/** Backfill: submit an already-generated clip without regenerating. */
export async function submitExistingClip(
  propertyId: string,
  roomIndex: number,
  blob: Blob,
): Promise<void> {
  beginRun(propertyId, []);
  patchSlot(propertyId, roomIndex, { status: 'ready' });
  await submitClip(propertyId, roomIndex, blob, new Map(), false);
  endRun(propertyId);
}
