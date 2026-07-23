import { getConversations } from '../../api/messages';
import { useAsync } from '../../hooks/useAsync';
import { useT } from '../../lib/i18n';
import AsyncBoundary from '../../components/AsyncBoundary';
import MessagesShell from '../../components/messages/MessagesShell';

export default function MessagesPage() {
  const state = useAsync(getConversations, []);
  const t = useT();

  return (
    <div className="flex h-[calc(100dvh-8.5rem)] min-h-[480px] flex-col sm:h-[calc(100dvh-10.5rem)]">
      <header className="mb-4">
        <h1 className="mb-1 text-3xl font-bold text-ink">Messages</h1>
        <p className="text-muted">Chat with tenants and applicants about your listings.</p>
      </header>
      <AsyncBoundary
        state={state}
        loadingMessage="Loading conversations…"
        errorMessage="Failed to load conversations."
        emptyMessage={t('No conversations yet.')}
        isEmpty={(r) => r.length === 0}
      >
        {(r) => <MessagesShell conversations={r} basePath="/landlord/messages" />}
      </AsyncBoundary>
    </div>
  );
}
