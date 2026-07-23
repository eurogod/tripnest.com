import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import type { Conversation } from '../../types';
import { markConversationRead } from '../../api/messages';
import { getSession } from '../../store/authStore';
import { joinConversation, onMessage } from '../../lib/chatHub';
import Card from '../ui/Card';
import { ChatIcon } from '../tenant/icons';
import ConversationList from './ConversationList';
import ChatThread from './ChatThread';

interface MessagesShellProps {
  conversations: Conversation[];
  /** Route prefix the shell navigates under: '/messages' or '/landlord/messages'. */
  basePath: string;
}

/**
 * Two-pane messages layout driven by the `:conversationId` route param.
 * Desktop shows list + thread side by side (falling back to the first
 * conversation when none is selected); mobile shows a single pane — the
 * list, or the thread with a back button once a conversation is opened.
 */
export default function MessagesShell({ conversations, basePath }: MessagesShellProps) {
  const { conversationId } = useParams();
  const navigate = useNavigate();
  // Local copy so unread pills can be cleared optimistically.
  const [rows, setRows] = useState(conversations);

  const selected = conversationId
    ? rows.find((c) => String(c.id) === conversationId)
    : undefined;
  // Desktop always shows a thread; an unknown id falls back to the first one.
  const shown = selected ?? rows[0];

  const clearUnread = (id: string) => {
    const wasUnread = rows.some((c) => String(c.id) === id && c.unread > 0);
    setRows((rs) => rs.map((c) => (String(c.id) === id ? { ...c, unread: 0 } : c)));
    if (wasUnread) markConversationRead(id).catch(() => {});
  };

  // Deep links (e.g. from a provider page) should also clear unread state.
  // Deferred a tick so the effect doesn't set state synchronously mid-render.
  useEffect(() => {
    if (!selected) return;
    const id = String(selected.id);
    const t = setTimeout(() => clearUnread(id), 0);
    return () => clearTimeout(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [conversationId]);

  // Live sidebar: join every conversation's hub group (idempotent) and fold
  // incoming messages into the list — preview, timestamp and unread pill.
  useEffect(() => {
    let active = true;
    conversations.forEach((c) => void joinConversation(String(c.id)).catch(() => {}));
    const off = onMessage((dto) => {
      if (!active) return;
      const me = getSession()?.userId ?? '';
      setRows((rs) => rs.map((c) => {
        if (String(c.id) !== dto.conversationId) return c;
        const isOpen = conversationId === dto.conversationId;
        return {
          ...c,
          lastMessage: dto.content,
          time: 'Just now',
          unread: dto.senderId === me || isOpen ? c.unread : c.unread + 1,
        };
      }));
    });
    return () => { active = false; off(); };
  }, [conversations, conversationId]);

  const open = (id: string) => {
    clearUnread(id);
    navigate(`${basePath}/${id}`);
  };

  return (
    <div className="grid min-h-0 flex-1 grid-cols-1 gap-4 md:grid-cols-[320px_1fr] xl:grid-cols-[350px_1fr]">
      <ConversationList
        conversations={rows}
        activeId={shown ? String(shown.id) : undefined}
        activeIsFallback={!selected}
        onSelect={open}
        className={selected ? 'hidden md:flex' : 'flex'}
      />
      {shown ? (
        <ChatThread
          key={String(shown.id)}
          conversation={shown}
          onBack={selected ? () => navigate(basePath) : undefined}
          className={selected ? 'flex' : 'hidden md:flex'}
        />
      ) : (
        <Card className="hidden min-h-0 flex-col items-center justify-center gap-3 p-8 text-center md:flex">
          <span className="flex h-14 w-14 items-center justify-center rounded-full bg-brand-50 text-brand">
            <ChatIcon size={26} />
          </span>
          <p className="font-semibold text-ink">Select a conversation</p>
          <p className="text-sm text-muted">Choose a conversation from the list to start chatting.</p>
        </Card>
      )}
    </div>
  );
}
