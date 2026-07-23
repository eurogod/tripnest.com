import {
  HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel,
} from '@microsoft/signalr';
import { API_ORIGIN, getAccessToken } from '../api/client';
import type { MessageResponseDto } from '../api/backend';

// ---------------------------------------------------------------------------
// Shared SignalR connection to TripNest.Core's ChatHub (/hubs/chat, proxied by
// Vite in dev). One connection per tab, started lazily by the first chat
// surface that mounts; JoinConversation subscribes the socket to a
// conversation's group so ReceiveMessage / typing / presence events flow in
// real time.
// ---------------------------------------------------------------------------

export interface PresenceUpdate {
  userId: string;
  isOnline: boolean;
  lastSeenAt: string | null;
}

interface TypingUpdate {
  conversationId: string;
  userId: string;
}

// In dev the Vite ws proxy can leave half-open upstream sockets when a page
// closes abruptly, which poisons the server's presence tracker (users stay
// "online" forever). The backend allows the dev origins via CORS, so connect
// straight to it — falling back to the proxied path if the direct origin is
// unreachable (e.g. the app is served from a port Core's CORS doesn't list).
const DIRECT_DEV_HUB = import.meta.env.DEV ? 'http://localhost:5091/hubs/chat' : null;
let useProxiedUrl = false;

function hubUrl(): string {
  if (DIRECT_DEV_HUB && !useProxiedUrl) return DIRECT_DEV_HUB;
  return `${API_ORIGIN}/hubs/chat`;
}

let connection: HubConnection | null = null;
let starting: Promise<HubConnection> | null = null;
const joined = new Set<string>();

type Handler<T> = (payload: T) => void;
const messageHandlers = new Set<Handler<MessageResponseDto>>();
const presenceHandlers = new Set<Handler<PresenceUpdate>>();
const typingHandlers = new Set<Handler<TypingUpdate>>();
const stopTypingHandlers = new Set<Handler<TypingUpdate>>();

function build(): HubConnection {
  const conn = new HubConnectionBuilder()
    .withUrl(hubUrl(), {
      accessTokenFactory: () => getAccessToken() ?? '',
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  conn.on('ReceiveMessage', (dto: MessageResponseDto) => {
    messageHandlers.forEach((h) => h(dto));
  });
  conn.on('PresenceChanged', (p: PresenceUpdate) => {
    presenceHandlers.forEach((h) => h(p));
  });
  conn.on('UserTyping', (t: TypingUpdate) => {
    typingHandlers.forEach((h) => h(t));
  });
  conn.on('UserStoppedTyping', (t: TypingUpdate) => {
    stopTypingHandlers.forEach((h) => h(t));
  });
  conn.on('Error', () => { /* hub-side validation failures — REST paths surface their own errors */ });

  // Group membership is per-connection: rejoin everything after a reconnect.
  conn.onreconnected(() => {
    joined.forEach((id) => { conn.invoke('JoinConversation', id).catch(() => {}); });
  });

  return conn;
}

/** The shared hub connection, connecting it on first use. */
export async function getChatConnection(): Promise<HubConnection> {
  if (connection?.state === HubConnectionState.Connected) return connection;
  if (starting) return starting;
  connection ??= build();
  if (connection.state === HubConnectionState.Disconnected) {
    starting = connection
      .start()
      .then(() => connection!)
      .catch(async (err) => {
        // Direct dev origin unreachable — retry once through the Vite proxy.
        if (DIRECT_DEV_HUB && !useProxiedUrl) {
          useProxiedUrl = true;
          connection = build();
          await connection.start();
          return connection;
        }
        throw err;
      })
      .finally(() => { starting = null; });
    return starting;
  }
  return connection;
}

/** Subscribe this socket to a conversation's live events (idempotent). */
export async function joinConversation(conversationId: string): Promise<void> {
  const conn = await getChatConnection();
  if (joined.has(conversationId)) return;
  joined.add(conversationId);
  await conn.invoke('JoinConversation', conversationId);
}

export function onMessage(handler: Handler<MessageResponseDto>): () => void {
  messageHandlers.add(handler);
  return () => messageHandlers.delete(handler);
}

export function onPresence(handler: Handler<PresenceUpdate>): () => void {
  presenceHandlers.add(handler);
  return () => presenceHandlers.delete(handler);
}

export function onTyping(handler: Handler<TypingUpdate>): () => void {
  typingHandlers.add(handler);
  return () => typingHandlers.delete(handler);
}

export function onStopTyping(handler: Handler<TypingUpdate>): () => void {
  stopTypingHandlers.add(handler);
  return () => stopTypingHandlers.delete(handler);
}

/** Current presence of a conversation partner (self and partners only). */
export async function getPresence(userId: string): Promise<PresenceUpdate | null> {
  try {
    const conn = await getChatConnection();
    return await conn.invoke<PresenceUpdate>('GetPresence', userId);
  } catch {
    return null;
  }
}

/** Fire-and-forget typing signals (never block the composer on the socket). */
export function sendTyping(conversationId: string): void {
  void getChatConnection().then((c) => c.invoke('Typing', conversationId)).catch(() => {});
}

export function sendStopTyping(conversationId: string): void {
  void getChatConnection().then((c) => c.invoke('StopTyping', conversationId)).catch(() => {});
}

/** Tear down on sign-out so the next user gets a fresh authenticated socket. */
export async function disconnectChat(): Promise<void> {
  joined.clear();
  if (connection) {
    const c = connection;
    connection = null;
    await c.stop().catch(() => {});
  }
}

if (typeof window !== 'undefined') {
  window.addEventListener('tripnest:unauthorized', () => { void disconnectChat(); });
}

// Dev-only: close the socket when HMR replaces this module, otherwise every
// hot update leaks a live connection and the server's presence tracker keeps
// the user "online" forever.
if (import.meta.hot) {
  import.meta.hot.dispose(() => { void disconnectChat(); });
}
