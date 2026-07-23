import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  aiErrorMessage, askAssistant, getAssistantHistory, type AssistantMessage,
} from '../../api/assistant';
import { useAsync } from '../../hooks/useAsync';
import { useFocusTrap } from '../../hooks/useFocusTrap';
import { useSession } from '../../store/authStore';
import { closeAssistant } from '../../store/assistantStore';
import AsyncBoundary from '../AsyncBoundary';
import Badge from '../ui/Badge';
import { ArrowUpIcon, SparkleIcon } from '../tenant/icons';

function Bubble({ message }: { message: AssistantMessage }) {
  return (
    <div className={`flex ${message.fromMe ? 'justify-end' : 'justify-start'}`}>
      <div
        className={`max-w-[85%] rounded-2xl px-4 py-2.5 text-sm shadow-sm ${
          message.fromMe
            ? 'rounded-br-md bg-brand text-white'
            : 'rounded-bl-md border border-gray-100 bg-white text-ink'
        }`}
      >
        <p className="whitespace-pre-wrap break-words">{message.text}</p>
        {message.supportTicketId && (
          <Badge tone="amber" className="mt-1.5">Escalated to support</Badge>
        )}
        <span className={`mt-1 block text-right text-[10px] ${message.fromMe ? 'text-white/70' : 'text-muted'}`}>
          {message.time}
        </span>
      </div>
    </div>
  );
}

/** Slide-over chat with the TripNest AI assistant (history + ask). */
export default function AssistantPanel() {
  const session = useSession();
  const navigate = useNavigate();
  const history = useAsync(() => getAssistantHistory(), []);
  const [pending, setPending] = useState<AssistantMessage[]>([]);
  const [draft, setDraft] = useState('');
  const [asking, setAsking] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [escalation, setEscalation] = useState<string | null>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const scrollRef = useRef<HTMLDivElement>(null);

  useFocusTrap(panelRef);

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') closeAssistant();
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, []);

  // Keep the latest message in view as the thread grows.
  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight });
  }, [pending, asking, history.data]);

  const ask = async () => {
    const question = draft.trim();
    if (question.length < 2 || question.length > 2000 || asking) return;
    const tempId = `pending-${Date.now()}`;
    setPending((p) => [...p, { id: tempId, fromMe: true, text: question, time: 'Now' }]);
    setDraft('');
    setError(null);
    setAsking(true);
    try {
      const reply = await askAssistant(question);
      setPending((p) => [
        ...p,
        { id: `${tempId}-answer`, fromMe: false, text: reply.answer, time: 'Now' },
      ]);
      if (reply.escalated && reply.supportConversationId) setEscalation(reply.supportConversationId);
    } catch (err) {
      // Give the user their question back to retry.
      setPending((p) => p.filter((m) => m.id !== tempId));
      setDraft((d) => d || question);
      setError(aiErrorMessage(err));
    } finally {
      setAsking(false);
    }
  };

  const openEscalation = (conversationId: string) => {
    closeAssistant();
    navigate(`${session?.role === 'landlord' ? '/landlord/messages' : '/messages'}/${conversationId}`);
  };

  return (
    <div className="fixed inset-0 z-50">
      <div className="absolute inset-0 bg-black/40" onClick={closeAssistant} aria-hidden="true" />
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-label="TripNest AI assistant"
        className="absolute inset-y-0 right-0 flex w-full max-w-md flex-col bg-gray-50 shadow-xl"
      >
        {/* Header */}
        <div className="flex items-center gap-3 border-b border-gray-100 bg-white px-5 py-4">
          <span className="flex h-10 w-10 items-center justify-center rounded-full bg-brand-50 text-brand">
            <SparkleIcon size={19} />
          </span>
          <div className="min-w-0 flex-1">
            <p className="font-semibold text-ink">Ask TripNest</p>
            <p className="text-xs text-muted">Answers about bookings, payments and your account</p>
          </div>
          <button
            type="button"
            onClick={closeAssistant}
            aria-label="Close assistant"
            className="rounded-lg px-2 py-1 text-xl leading-none text-muted transition-colors hover:bg-gray-100 hover:text-ink"
          >
            ×
          </button>
        </div>

        {escalation && (
          <div className="border-b border-amber-200 bg-amber-50 px-5 py-3 text-sm text-amber-800">
            <p className="font-semibold">A support agent has been looped in.</p>
            <button
              type="button"
              onClick={() => openEscalation(escalation)}
              className="mt-1 font-semibold text-brand hover:underline"
            >
              Open the conversation →
            </button>
          </div>
        )}

        {/* Thread */}
        <div ref={scrollRef} className="flex-1 overflow-y-auto px-4 py-5">
          <AsyncBoundary
            state={history}
            loadingMessage="Loading your conversation…"
            errorMessage="Couldn't load your conversation."
          >
            {(messages) => (
              <div className="space-y-2.5">
                {messages.length === 0 && pending.length === 0 && (
                  <div className="rounded-2xl border border-gray-100 bg-white px-4 py-3 text-sm text-muted">
                    Hi{session ? ` ${session.name.split(' ')[0]}` : ''}! Ask me anything about your
                    bookings, payments, verification or how TripNest works. If you need a human,
                    I'll connect you to support.
                  </div>
                )}
                {[...messages, ...pending].map((m) => <Bubble key={m.id} message={m} />)}
                {asking && (
                  <div className="flex justify-start">
                    <div className="rounded-2xl rounded-bl-md border border-gray-100 bg-white px-4 py-2.5 shadow-sm">
                      <SparkleIcon size={16} className="animate-pulse text-brand" />
                    </div>
                  </div>
                )}
              </div>
            )}
          </AsyncBoundary>
        </div>

        {/* Composer */}
        <div className="border-t border-gray-100 bg-white p-3">
          {error && (
            <p className="mb-2 px-1 text-sm text-rose-600" role="alert">{error}</p>
          )}
          <div className="flex items-center gap-2 rounded-2xl border border-gray-200 bg-white px-3 py-2 transition-colors focus-within:border-brand">
            <input
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              onKeyDown={(e) => { if (e.key === 'Enter') void ask(); }}
              disabled={asking}
              maxLength={2000}
              placeholder={asking ? 'Thinking…' : 'Ask a question…'}
              className="w-full bg-transparent px-1 py-1.5 text-sm text-ink outline-none placeholder:text-muted disabled:opacity-60"
            />
            <button
              type="button"
              onClick={() => void ask()}
              disabled={asking || draft.trim().length < 2}
              aria-label="Send question"
              className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-brand text-white transition-opacity hover:bg-brand/90 disabled:opacity-40"
            >
              <ArrowUpIcon size={18} />
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
