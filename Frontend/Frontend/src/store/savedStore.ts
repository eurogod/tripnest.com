import { useSyncExternalStore } from 'react';
import { apiGetList } from '../api/client';
import type { WishlistItemDto } from '../api/backend';
import { saveProperty, unsaveProperty } from '../api/properties';
import { getSession } from './authStore';

// ---------------------------------------------------------------------------
// The signed-in user's wishlist as a live Set of property ids, shared by every
// heart button (search cards, saved page, property detail). Loaded once per
// user from /api/wishlist/mine; toggles apply optimistically and roll back if
// the API call fails.
// ---------------------------------------------------------------------------

let ids: ReadonlySet<string> | null = null; // null = not loaded yet
let loadedFor: string | null = null; // userId the current set belongs to
let loading: Promise<void> | null = null;
const listeners = new Set<() => void>();

const notify = () => listeners.forEach((l) => l());

function ensureLoaded(): void {
  const userId = getSession()?.userId ?? null;
  if (!userId) return;
  if (loadedFor === userId || loading) return;
  loading = apiGetList<WishlistItemDto>('/api/wishlist/mine')
    .then((items) => {
      ids = new Set(items.map((i) => i.propertyId));
      loadedFor = userId;
      notify();
    })
    .catch(() => { /* stays null — hearts fall back to their initialSaved hint */ })
    .finally(() => { loading = null; });
}

if (typeof window !== 'undefined') {
  window.addEventListener('tripnest:unauthorized', () => {
    ids = null;
    loadedFor = null;
    notify();
  });
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  ensureLoaded(); // consumers mounting is the natural load (and re-load after user switch) trigger
  return () => { listeners.delete(listener); };
}

/** The saved-property id set, or null while it hasn't loaded. */
export function useSavedIds(): ReadonlySet<string> | null {
  return useSyncExternalStore(subscribe, () => ids, () => null);
}

/**
 * Optimistically save/unsave a property, persisting via the wishlist API.
 * Resolves to the final saved state (reverted if the server rejected it).
 */
export async function toggleSaved(propertyId: string): Promise<boolean> {
  const wasSaved = ids?.has(propertyId) ?? false;
  const next = new Set(ids ?? []);
  if (wasSaved) next.delete(propertyId);
  else next.add(propertyId);
  ids = next;
  notify();
  try {
    await (wasSaved ? unsaveProperty(propertyId) : saveProperty(propertyId));
    return !wasSaved;
  } catch {
    const reverted = new Set(ids);
    if (wasSaved) reverted.add(propertyId);
    else reverted.delete(propertyId);
    ids = reverted;
    notify();
    return wasSaved;
  }
}
