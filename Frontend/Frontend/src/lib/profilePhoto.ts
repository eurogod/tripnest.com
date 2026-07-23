// Client-side cache of the user's own profile photo.
//
// POST /api/profile/photo persists the file and GET /api/profile/me returns
// its ProfilePhotoPath, but Core does not serve /uploads statically, so the
// stored path 404s in the browser. Until it does, keep a downscaled copy of
// what this browser uploaded so avatars can show it. The server copy remains
// the source of truth (it already appears on the TripNest ID card PDF).

const KEY_PREFIX = 'tripnest.profilePhoto.';
const MAX_EDGE_PX = 512;
const JPEG_QUALITY = 0.85;

export async function cacheProfilePhoto(userId: string, file: File): Promise<void> {
  try {
    const bitmap = await createImageBitmap(file);
    const scale = Math.min(1, MAX_EDGE_PX / Math.max(bitmap.width, bitmap.height));
    const canvas = document.createElement('canvas');
    canvas.width = Math.max(1, Math.round(bitmap.width * scale));
    canvas.height = Math.max(1, Math.round(bitmap.height * scale));
    canvas.getContext('2d')?.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
    bitmap.close();
    localStorage.setItem(KEY_PREFIX + userId, canvas.toDataURL('image/jpeg', JPEG_QUALITY));
  } catch {
    // Storage quota or decode failure — the server copy still exists.
  }
}

export function getCachedProfilePhoto(userId: string | undefined): string | undefined {
  if (!userId) return undefined;
  try {
    return localStorage.getItem(KEY_PREFIX + userId) ?? undefined;
  } catch {
    return undefined;
  }
}
