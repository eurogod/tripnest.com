// Client-side cache of uploaded listing photos.
//
// TripNest.Core stores uploaded photos (files + PropertyPhoto rows) but does
// not yet return them on the property DTO (PhotoPaths stays null) nor serve
// /uploads statically. Until it does, we keep downscaled copies of what this
// browser uploaded so galleries and the video walkthrough can show them.
// Server copies remain the source of truth once Core exposes them.

const KEY_PREFIX = 'tripnest.photos.';
const MAX_EDGE_PX = 1080;
const JPEG_QUALITY = 0.8;

async function toDataUrl(file: File): Promise<string> {
  const bitmap = await createImageBitmap(file);
  const scale = Math.min(1, MAX_EDGE_PX / Math.max(bitmap.width, bitmap.height));
  const canvas = document.createElement('canvas');
  canvas.width = Math.max(1, Math.round(bitmap.width * scale));
  canvas.height = Math.max(1, Math.round(bitmap.height * scale));
  canvas.getContext('2d')?.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
  bitmap.close();
  return canvas.toDataURL('image/jpeg', JPEG_QUALITY);
}

export async function cacheListingPhotos(propertyId: string, files: File[]): Promise<void> {
  try {
    const urls = await Promise.all(files.map(toDataUrl));
    localStorage.setItem(KEY_PREFIX + propertyId, JSON.stringify(urls));
  } catch {
    // Storage quota or decode failure — the server copies still exist.
  }
}

/**
 * Rehydrate this browser's cached photos as Files, so walkthrough generation
 * can run for listings created earlier (their photos never round-trip
 * through the backend — see the module comment).
 */
export async function cachedPhotoFiles(propertyId: string): Promise<File[]> {
  const urls = getCachedListingPhotos(propertyId);
  if (!urls) return [];
  return Promise.all(urls.map(async (dataUrl, i) => {
    const blob = await (await fetch(dataUrl)).blob();
    return new File([blob], `photo-${i + 1}.jpg`, { type: blob.type || 'image/jpeg' });
  }));
}

export function getCachedListingPhotos(propertyId: string): string[] | undefined {
  try {
    const raw = localStorage.getItem(KEY_PREFIX + propertyId);
    const urls = raw ? (JSON.parse(raw) as string[]) : undefined;
    return urls && urls.length > 0 ? urls : undefined;
  } catch {
    return undefined;
  }
}
