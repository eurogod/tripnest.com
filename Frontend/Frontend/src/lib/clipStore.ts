// IndexedDB store for generated walkthrough clips (MP4 blobs are far too
// large for localStorage). Keyed `${propertyId}:${roomIndex}` to line up
// with the photo→room assignment in buildTour.

const DB_NAME = 'tripnest-walkthroughs';
const STORE = 'clips';

export interface StoredClip {
  propertyId: string;
  roomIndex: number;
  blob: Blob;
  sourcePhoto?: string;
  generatedAt: string;
  provider?: 'google-flow' | 'local';
}

function openDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, 1);
    req.onupgradeneeded = () => {
      req.result.createObjectStore(STORE);
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

function key(propertyId: string, roomIndex: number): string {
  return `${propertyId}:${roomIndex}`;
}

export async function putClip(clip: StoredClip): Promise<void> {
  const db = await openDb();
  await new Promise<void>((resolve, reject) => {
    const tx = db.transaction(STORE, 'readwrite');
    tx.objectStore(STORE).put(clip, key(clip.propertyId, clip.roomIndex));
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
  db.close();
}

/** All stored clips for a property, keyed by room index. */
export async function getClipsForProperty(propertyId: string): Promise<Map<number, StoredClip>> {
  const db = await openDb();
  const clips = await new Promise<Map<number, StoredClip>>((resolve, reject) => {
    const range = IDBKeyRange.bound(`${propertyId}:`, `${propertyId}:￿`);
    const req = db.transaction(STORE, 'readonly').objectStore(STORE).getAll(range);
    req.onsuccess = () => {
      const map = new Map<number, StoredClip>();
      for (const clip of req.result as StoredClip[]) map.set(clip.roomIndex, clip);
      resolve(map);
    };
    req.onerror = () => reject(req.error);
  });
  db.close();
  return clips;
}

export async function hasClip(propertyId: string, roomIndex: number): Promise<boolean> {
  const db = await openDb();
  const found = await new Promise<boolean>((resolve, reject) => {
    const req = db.transaction(STORE, 'readonly').objectStore(STORE).getKey(key(propertyId, roomIndex));
    req.onsuccess = () => resolve(req.result !== undefined);
    req.onerror = () => reject(req.error);
  });
  db.close();
  return found;
}
