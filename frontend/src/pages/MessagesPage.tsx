import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { chatApi } from '@/lib/services';
import { PageHeader } from '@/components/dashboard';
import { EmptyState, Spinner } from '@/components/ui';
import { Avatar } from '@/components/badges';
import { Chat } from '@/components/icons';
import { relativeTime } from '@/lib/format';
import { useAuth } from '@/auth/AuthContext';

export default function MessagesPage() {
  const { user } = useAuth();
  const qc = useQueryClient();
  const [active, setActive] = useState<string | null>(null);
  const [draft, setDraft] = useState('');
  const endRef = useRef<HTMLDivElement>(null);

  const convos = useQuery({ queryKey: ['conversations'], queryFn: chatApi.conversations, enabled: !!user });
  const thread = useQuery({
    queryKey: ['messages', active],
    queryFn: () => chatApi.messages(active!, 1, 50),
    enabled: !!active,
    refetchInterval: active ? 15_000 : false,
  });

  const send = useMutation({
    mutationFn: (body: string) => chatApi.send(active!, body),
    onSuccess: () => {
      setDraft('');
      qc.invalidateQueries({ queryKey: ['messages', active] });
    },
  });

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [thread.data]);

  const other = (c: { user1Id: string; user2Id: string }) => (c.user1Id === user?.userId ? c.user2Id : c.user1Id);

  return (
    <div className="container-tn max-w-6xl py-8">
      <PageHeader title="Messages" subtitle="Talk to hosts, agents and caretakers — keep it on TripNest for your protection." />

      <div className="grid h-[70vh] grid-cols-1 overflow-hidden rounded-xl border border-line bg-white md:grid-cols-[20rem_1fr]">
        {/* Conversation list */}
        <aside className={`border-r border-line ${active ? 'hidden md:block' : ''}`}>
          {convos.isLoading ? (
            <div className="grid h-full place-items-center text-brand-600">
              <Spinner className="h-6 w-6" />
            </div>
          ) : !convos.data?.length ? (
            <div className="grid h-full place-items-center p-6 text-center text-sm text-muted">No conversations yet.</div>
          ) : (
            <ul className="h-full overflow-y-auto">
              {(convos.data ?? []).map((c) => (
                <li key={c.conversationId}>
                  <button
                    onClick={() => setActive(c.conversationId)}
                    className={`flex w-full items-center gap-3 border-b border-line px-4 py-3 text-left transition hover:bg-black/5 ${
                      active === c.conversationId ? 'bg-brand-50' : ''
                    }`}
                  >
                    <Avatar name={other(c)} size={40} />
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-semibold">Conversation</p>
                      <p className="truncate text-xs text-muted">{relativeTime(c.lastMessageAt ?? c.createdAt)}</p>
                    </div>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </aside>

        {/* Thread */}
        <section className="flex min-h-0 flex-col">
          {!active ? (
            <div className="grid flex-1 place-items-center p-6">
              <EmptyState icon={<Chat className="h-6 w-6" />} title="Pick a conversation" subtitle="Choose someone on the left to see your messages." />
            </div>
          ) : (
            <>
              <div className="flex-1 space-y-2 overflow-y-auto bg-surface p-4">
                {thread.isLoading ? (
                  <div className="grid h-full place-items-center text-brand-600">
                    <Spinner className="h-6 w-6" />
                  </div>
                ) : (
                  [...(thread.data?.items ?? [])].reverse().map((m) => {
                    const mine = m.senderId === user?.userId;
                    return (
                      <div key={m.messageId} className={`flex ${mine ? 'justify-end' : 'justify-start'}`}>
                        <div
                          className={`max-w-[75%] rounded-2xl px-3.5 py-2 text-sm ${
                            mine ? 'bg-brand-600 text-white' : 'bg-white text-ink shadow-card'
                          }`}
                        >
                          <p>{m.content}</p>
                          <p className={`mt-0.5 text-[10px] ${mine ? 'text-white/70' : 'text-muted'}`}>
                            {relativeTime(m.createdAt)}
                          </p>
                        </div>
                      </div>
                    );
                  })
                )}
                <div ref={endRef} />
              </div>
              <form
                className="flex items-center gap-2 border-t border-line p-3"
                onSubmit={(e) => {
                  e.preventDefault();
                  if (draft.trim()) send.mutate(draft.trim());
                }}
              >
                <input
                  value={draft}
                  onChange={(e) => setDraft(e.target.value)}
                  placeholder="Write a message…"
                  className="input"
                />
                <button type="submit" disabled={!draft.trim() || send.isPending} className="btn-primary shrink-0">
                  {send.isPending ? <Spinner /> : 'Send'}
                </button>
              </form>
            </>
          )}
        </section>
      </div>
    </div>
  );
}
