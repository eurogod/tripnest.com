import { useEffect, useRef, useState } from 'react';
import type { Conversation } from '../../types';
import { getConversations } from '../../api/messages';
import Avatar from '../ui/Avatar';

interface MessagesPopoverProps {
  onClose: () => void;
  onSelect: (conversation: Conversation) => void;
  onSeeAll: () => void;
}

/** Anchored dropdown previewing recent conversations; opening one hands off to the full chat thread. */
export default function MessagesPopover({ onClose, onSelect, onSeeAll }: MessagesPopoverProps) {
  const [rows, setRows] = useState<Conversation[] | null>(null);
  const [error, setError] = useState(false);
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    let active = true;
    getConversations()
      .then((data) => { if (active) setRows(data); })
      .catch(() => { if (active) setError(true); });
    return () => { active = false; };
  }, []);

  useEffect(() => {
    const onPointerDown = (e: MouseEvent) => {
      if (panelRef.current && !panelRef.current.contains(e.target as Node)) onClose();
    };
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('mousedown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('mousedown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [onClose]);

  return (
    <div
      ref={panelRef}
      role="dialog"
      aria-label="Messages"
      className="absolute right-0 top-full z-[60] mt-3 w-80 max-w-[calc(100vw-2rem)] overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-xl"
    >
      <div className="flex items-center justify-between border-b border-gray-100 px-4 py-3">
        <p className="font-semibold text-ink">Messages</p>
        <button onClick={onSeeAll} className="text-xs font-semibold text-brand hover:underline">
          See all
        </button>
      </div>

      <div className="max-h-96 overflow-y-auto">
        {rows === null && !error && (
          <p className="px-4 py-6 text-center text-sm text-muted">Loading…</p>
        )}
        {error && (
          <p className="px-4 py-6 text-center text-sm text-muted">Couldn&apos;t load messages.</p>
        )}
        {rows && rows.length === 0 && (
          <p className="px-4 py-6 text-center text-sm text-muted">No conversations yet.</p>
        )}
        {rows?.map((c) => (
          <button
            key={c.id}
            onClick={() => onSelect(c)}
            className={`flex w-full items-center gap-3 border-b border-gray-50 px-4 py-3 text-left transition-colors last:border-b-0 hover:bg-gray-50 ${
              c.unread > 0 ? 'bg-brand-50/40' : ''
            }`}
          >
            <Avatar name={c.name} size={40} />
            <div className="min-w-0 flex-1">
              <div className="flex items-center justify-between gap-2">
                <p className="truncate text-sm font-semibold text-ink">{c.name}</p>
                <span className="shrink-0 text-xs text-muted">{c.time}</span>
              </div>
              <p className="truncate text-xs text-muted">{c.lastMessage}</p>
            </div>
            {c.unread > 0 && (
              <span className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-brand" />
            )}
          </button>
        ))}
      </div>
    </div>
  );
}
