import { getConversations } from '../../api/messages';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../AsyncBoundary';
import MessagesShell from '../messages/MessagesShell';

/** Shared Messages page for the agent / caretaker / admin workspaces. */
export default function WorkspaceMessagesPage({ basePath, subtitle }: {
  basePath: string;
  subtitle: string;
}) {
  const state = useAsync(getConversations, []);

  return (
    <div className="flex h-[calc(100dvh-8.5rem)] min-h-[480px] flex-col sm:h-[calc(100dvh-10.5rem)]">
      <header className="mb-4">
        <h1 className="mb-1 text-3xl font-bold text-ink">Messages</h1>
        <p className="text-muted">{subtitle}</p>
      </header>
      <AsyncBoundary
        state={state}
        loadingMessage="Loading conversations…"
        errorMessage="Failed to load conversations."
        emptyMessage="No conversations yet — chats started with you appear here."
        isEmpty={(r) => r.length === 0}
      >
        {(r) => <MessagesShell conversations={r} basePath={basePath} />}
      </AsyncBoundary>
    </div>
  );
}
