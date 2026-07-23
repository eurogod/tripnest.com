// Known chat contacts (userId → display name/role), persisted locally.
// The chat DTOs carry user ids only and Core exposes no user-lookup
// endpoint, so every flow that knows who it is opening a chat with
// records the name here for the conversation list to use — the same
// workaround pattern as lib/listingPhotos.ts.

const KEY = 'tripnest.chatContacts';

export interface ChatContact {
  name: string;
  role?: string;
}

function read(): Record<string, ChatContact> {
  try {
    return JSON.parse(localStorage.getItem(KEY) ?? '{}') as Record<string, ChatContact>;
  } catch {
    return {};
  }
}

export function rememberContact(userId: string, name: string, role?: string): void {
  if (!userId || !name) return;
  try {
    const all = read();
    all[userId] = { name, role };
    localStorage.setItem(KEY, JSON.stringify(all));
  } catch { /* ignore */ }
}

export function getContact(userId: string): ChatContact | undefined {
  return read()[userId];
}
