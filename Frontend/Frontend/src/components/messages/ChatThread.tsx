import { useEffect, useRef, useState } from 'react';
import type { Conversation } from '../../types';
import { getMessages, sendAttachment, sendMessage, suggestReply } from '../../api/messages';
import { mapMessage, timeAgo } from '../../api/backend';
import { getSession } from '../../store/authStore';
import {
  joinConversation, onMessage, onPresence, onTyping, onStopTyping,
  getPresence, sendTyping, sendStopTyping, type PresenceUpdate,
} from '../../lib/chatHub';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../AsyncBoundary';
import Card from '../ui/Card';
import Avatar from '../ui/Avatar';
import { ChevronLeftIcon, PhoneIcon, InfoIcon } from '../tenant/icons';
import type { ChatItem } from './types';
import VoicePlayer from './VoicePlayer';
import Composer from './Composer';
import ThreadDetails from './ThreadDetails';

/** Header action: small round icon button with an accessible label. */
function HeaderAction({ label, onClick, active, children }: {
  label: string; onClick: () => void; active?: boolean; children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={label}
      aria-pressed={active}
      className={`flex h-9 w-9 items-center justify-center rounded-full transition-colors ${
        active ? 'bg-brand-50 text-brand' : 'text-muted hover:bg-gray-100 hover:text-ink'
      }`}
    >
      {children}
    </button>
  );
}

interface ChatThreadProps {
  conversation: Conversation;
  /** When provided, a mobile-only back button appears in the header. */
  onBack?: () => void;
  className?: string;
}

/** The open conversation: header, message history, composer, details aside. */
export default function ChatThread({ conversation, onBack, className = '' }: ChatThreadProps) {
  const state = useAsync(() => getMessages(conversation.id), [conversation.id]);
  const [sent, setSent] = useState<ChatItem[]>([]);
  const [showDetails, setShowDetails] = useState(false);
  const [banner, setBanner] = useState<string | null>(null);
  const [presence, setPresence] = useState<PresenceUpdate | null>(null);
  const [partnerTyping, setPartnerTyping] = useState(false);
  const scrollRef = useRef<HTMLDivElement>(null);

  // Keep the latest message in view as the thread grows or switches.
  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight });
  }, [sent, state.data, partnerTyping]);

  // Auto-dismiss the transient banner.
  useEffect(() => {
    if (!banner) return;
    const t = setTimeout(() => setBanner(null), 2500);
    return () => clearTimeout(t);
  }, [banner]);

  // Live events for this conversation: incoming messages and typing signals.
  useEffect(() => {
    const convId = String(conversation.id);
    let active = true;
    void joinConversation(convId).catch(() => { /* REST history still renders */ });
    const offMessage = onMessage((dto) => {
      if (!active || dto.conversationId !== convId) return;
      setPartnerTyping(false);
      setSent((s) => {
        if (s.some((m) => m.id === dto.messageId)) return s; // REST echo already shown
        return [...s, mapMessage(dto, getSession()?.userId ?? '')];
      });
    });
    const offTyping = onTyping((t) => { if (active && t.conversationId === convId) setPartnerTyping(true); });
    const offStop = onStopTyping((t) => { if (active && t.conversationId === convId) setPartnerTyping(false); });
    return () => { active = false; offMessage(); offTyping(); offStop(); };
  }, [conversation.id]);

  // Stuck "typing…" guard: hub sends StopTyping, but clear it locally too.
  useEffect(() => {
    if (!partnerTyping) return;
    const t = setTimeout(() => setPartnerTyping(false), 6000);
    return () => clearTimeout(t);
  }, [partnerTyping]);

  // Live presence of the counterpart. PresenceChanged pushes use SignalR's
  // Clients.Users(), which Core's JWT never reaches (the token carries a raw
  // "sub" claim and .NET 8 doesn't map it to NameIdentifier — see
  // must_fix.md), so a light poll keeps the status honest; the subscription
  // stays for whenever the server-side mapping is fixed.
  useEffect(() => {
    const other = conversation.otherUserId;
    if (!other) return;
    let active = true;
    const pull = () => { void getPresence(other).then((p) => { if (active && p) setPresence(p); }); };
    pull();
    const poll = setInterval(pull, 20_000);
    const off = onPresence((p) => { if (active && p.userId === other) setPresence(p); });
    return () => { active = false; clearInterval(poll); off(); };
  }, [conversation.otherUserId]);

  // Throttled typing signals from the composer.
  const lastTypingAt = useRef(0);
  const stopTypingTimer = useRef<ReturnType<typeof setTimeout>>(undefined);
  const handleTyping = () => {
    const convId = String(conversation.id);
    const now = Date.now();
    if (now - lastTypingAt.current > 2500) {
      lastTypingAt.current = now;
      sendTyping(convId);
    }
    clearTimeout(stopTypingTimer.current);
    stopTypingTimer.current = setTimeout(() => sendStopTyping(convId), 3000);
  };

  const send = async (text: string): Promise<boolean> => {
    clearTimeout(stopTypingTimer.current);
    sendStopTyping(String(conversation.id));
    // Optimistic bubble, reconciled with the saved message from the API.
    const tempId = `pending-${Date.now()}`;
    setSent((s) => [...s, { id: tempId, fromMe: true, time: 'Sending…', text }]);
    try {
      const saved = await sendMessage(conversation.id, text);
      setSent((s) => {
        // The hub echo can land before the REST response — don't double up.
        if (s.some((m) => m.id === saved.id)) return s.filter((m) => m.id !== tempId);
        return s.map((m) => (m.id === tempId ? saved : m));
      });
      return true;
    } catch {
      setSent((s) => s.filter((m) => m.id !== tempId));
      setBanner('Message failed to send.');
      return false;
    }
  };

  const sentIds = new Set(sent.map((m) => m.id));
  const statusLine = partnerTyping
    ? 'typing…'
    : presence
      ? presence.isOnline
        ? 'Online'
        : presence.lastSeenAt
          ? `Last seen ${timeAgo(presence.lastSeenAt)}`
          : 'Offline'
      : conversation.role;

  return (
    <Card className={`tn-card-in flex min-h-0 overflow-hidden ${className}`}>
      <div className="flex min-w-0 flex-1 flex-col">
        {/* Header */}
        <div className="flex items-center gap-3 border-b border-gray-100 px-4 py-3 sm:px-5">
          {onBack && (
            <button
              type="button"
              onClick={onBack}
              aria-label="Back to conversations"
              className="-ml-1 flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-muted hover:bg-gray-100 hover:text-ink md:hidden"
            >
              <ChevronLeftIcon size={18} />
            </button>
          )}
          <div className="relative">
            <Avatar name={conversation.name} size={40} />
            {presence?.isOnline && (
              <span className="absolute -bottom-0.5 -right-0.5 h-3 w-3 rounded-full border-2 border-white bg-emerald-500" />
            )}
          </div>
          <div className="min-w-0 flex-1">
            <p className="truncate font-semibold text-ink">{conversation.name}</p>
            <p className={`truncate text-xs ${partnerTyping ? 'font-medium text-brand' : presence?.isOnline ? 'text-emerald-600' : 'text-muted'}`}>
              {statusLine}{statusLine === conversation.role ? '' : ` · ${conversation.role}`}
            </p>
          </div>
          <HeaderAction label={`Call ${conversation.name}`} onClick={() => setBanner(`Calling ${conversation.name}…`)}>
            <PhoneIcon size={17} />
          </HeaderAction>
          <HeaderAction label="Conversation details" active={showDetails} onClick={() => setShowDetails((v) => !v)}>
            <InfoIcon size={18} />
          </HeaderAction>
        </div>

        {banner && (
          <div className="border-b border-brand-50 bg-brand-50 px-5 py-2 text-center text-sm font-medium text-brand">
            {banner}
          </div>
        )}

        {/* Messages */}
        <div ref={scrollRef} className="flex-1 overflow-y-auto bg-gray-50/50 px-4 py-5">
          <div className="mx-auto max-w-2xl">
            <p className="mb-4 text-center text-[11px] font-semibold uppercase tracking-wider text-gray-400">
              Today
            </p>
            <AsyncBoundary state={state} loadingMessage="Loading messages…" errorMessage="Failed to load messages.">
              {(history) => (
                <div className="space-y-2.5">
                  {[
                    ...history,
                    // Live/optimistic items not already in the fetched history.
                    ...(sent as ChatItem[]).filter((m) => !history.some((h) => h.id === m.id)),
                  ].map((m) => {
                    const item = m as ChatItem;
                    return (
                      <div
                        key={item.id}
                        className={`flex ${item.fromMe ? 'justify-end' : 'justify-start'} ${
                          sentIds.has(item.id) ? 'tn-rise' : ''
                        }`}
                      >
                        <div
                          className={`max-w-[78%] rounded-2xl px-4 py-2.5 text-sm shadow-sm ${
                            item.fromMe
                              ? 'rounded-br-md bg-brand text-white'
                              : 'rounded-bl-md border border-gray-100 bg-white text-ink'
                          }`}
                        >
                          {item.audioUrl ? (
                            <VoicePlayer url={item.audioUrl} duration={item.duration ?? 0} mine={item.fromMe} />
                          ) : item.mediaUrl && item.mediaType?.startsWith('audio') ? (
                            <VoicePlayer url={item.mediaUrl} duration={item.duration ?? 0} mine={item.fromMe} />
                          ) : item.mediaUrl && item.mediaType?.startsWith('image') ? (
                            <a href={item.mediaUrl} target="_blank" rel="noreferrer">
                              <img src={item.mediaUrl} alt={item.text || 'Attachment'} className="max-h-48 rounded-lg" />
                            </a>
                          ) : item.mediaUrl ? (
                            <a href={item.mediaUrl} target="_blank" rel="noreferrer" className="break-all underline">
                              {item.text || 'Attachment'}
                            </a>
                          ) : (
                            <p className="whitespace-pre-wrap break-words">{item.text}</p>
                          )}
                          <span className={`mt-1 block text-right text-[10px] ${item.fromMe ? 'text-white/70' : 'text-muted'}`}>
                            {item.time}
                          </span>
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </AsyncBoundary>
          </div>
        </div>

        <Composer
          onSendText={send}
          onSendVoice={async (audioUrl, duration) => {
            // Local bubble immediately; the recording then uploads like any attachment.
            const tempId = `pending-${Date.now()}`;
            setSent((s) => [...s, { id: tempId, fromMe: true, time: 'Now', text: '', audioUrl, duration }]);
            try {
              const blob = await fetch(audioUrl).then((r) => r.blob());
              const ext = (blob.type.split('/')[1] || 'webm').split(';')[0];
              const saved = await sendAttachment(conversation.id, new File([blob], `voice-note.${ext}`, { type: blob.type || 'audio/webm' }));
              setSent((s) => {
                // The hub echo may already have rendered the saved message.
                if (s.some((m) => m.id === saved.id)) return s.filter((m) => m.id !== tempId);
                // Adopt the saved id (so the echo dedupes) but keep the local blob for playback.
                return s.map((m) => (m.id === tempId ? { ...saved, audioUrl, duration } : m));
              });
            } catch {
              setSent((s) => s.filter((m) => m.id !== tempId));
              setBanner('Voice note failed to send.');
            }
          }}
          onAttach={async (file) => {
            const tempId = `pending-${Date.now()}`;
            setSent((s) => [...s, { id: tempId, fromMe: true, time: 'Sending…', text: `📎 ${file.name}` }]);
            try {
              const saved = await sendAttachment(conversation.id, file);
              setSent((s) => {
                if (s.some((m) => m.id === saved.id)) return s.filter((m) => m.id !== tempId);
                return s.map((m) => (m.id === tempId ? saved : m));
              });
            } catch {
              setSent((s) => s.filter((m) => m.id !== tempId));
              setBanner('Attachment failed to send.');
            }
          }}
          onNotice={setBanner}
          suggest={() => suggestReply(conversation.id)}
          onTyping={handleTyping}
        />
      </div>

      {showDetails && (
        <ThreadDetails conversation={conversation} onCall={() => setBanner(`Calling ${conversation.name}…`)} />
      )}
    </Card>
  );
}
