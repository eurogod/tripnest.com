import { useMemo, useState } from 'react';
import type { Conversation } from '../../types';
import Card from '../ui/Card';
import Avatar from '../ui/Avatar';
import { SearchIcon } from '../tenant/icons';

interface ConversationListProps {
  conversations: Conversation[];
  /** String(conversation.id) of the open thread, if any. */
  activeId?: string;
  /** The active thread is only a desktop fallback — highlight it on md+ only. */
  activeIsFallback?: boolean;
  onSelect: (id: string) => void;
  className?: string;
}

/** Searchable conversation sidebar shared by the tenant and landlord pages. */
export default function ConversationList({ conversations, activeId, activeIsFallback, onSelect, className = '' }: ConversationListProps) {
  const [query, setQuery] = useState('');

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return q
      ? conversations.filter((c) => c.name.toLowerCase().includes(q) || c.role.toLowerCase().includes(q))
      : conversations;
  }, [conversations, query]);

  return (
    <Card className={`tn-card-in min-h-0 flex-col overflow-hidden ${className}`}>
      <div className="relative border-b border-gray-100 p-3">
        <span className="pointer-events-none absolute left-6 top-1/2 -translate-y-1/2 text-muted">
          <SearchIcon size={16} />
        </span>
        <input
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Search conversations…"
          className="w-full rounded-full border border-gray-200 bg-gray-50 py-2 pl-9 pr-3 text-sm text-ink outline-none focus:border-brand"
        />
      </div>
      <div className="flex-1 divide-y divide-gray-100 overflow-y-auto">
        {filtered.length === 0 ? (
          <p className="px-4 py-6 text-center text-sm text-muted">No conversations found.</p>
        ) : (
          filtered.map((c) => {
            const active = String(c.id) === activeId;
            const highlight = active
              ? activeIsFallback
                ? 'border-transparent hover:bg-gray-50 md:border-brand md:bg-brand-50'
                : 'border-brand bg-brand-50'
              : 'border-transparent hover:bg-gray-50';
            return (
              <button
                key={c.id}
                onClick={() => onSelect(String(c.id))}
                className={`flex w-full items-center gap-3 border-l-2 px-4 py-3 text-left transition-colors ${highlight}`}
              >
                <Avatar name={c.name} size={40} />
                <span className="min-w-0 flex-1">
                  <span className="flex items-center justify-between">
                    <span className="truncate text-sm font-semibold text-ink">{c.name}</span>
                    <span className="shrink-0 pl-2 text-xs text-muted">{c.time}</span>
                  </span>
                  <span className={`block truncate text-xs ${c.unread > 0 ? 'font-medium text-ink' : 'text-muted'}`}>
                    {c.lastMessage}
                  </span>
                </span>
                {c.unread > 0 && (
                  <span className="flex h-5 min-w-5 items-center justify-center rounded-full bg-brand px-1.5 text-[11px] font-semibold text-white">
                    {c.unread}
                  </span>
                )}
              </button>
            );
          })
        )}
      </div>
    </Card>
  );
}
