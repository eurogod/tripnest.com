import { useState } from 'react';
import { getSupportTickets, resolveSupportTicket, type SupportTicketDto } from '../../api/adminWorkspace';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import Badge from '../../components/ui/Badge';
import Avatar from '../../components/ui/Avatar';
import { formatIsoDateFull } from '../../api/backend';

export default function SupportTicketsPage() {
  const state = useAsync(getSupportTickets);

  return (
    <div>
      <h1 className="text-3xl font-bold tracking-tight text-ink">Support tickets</h1>
      <p className="mt-1 mb-6 text-sm text-muted">Assistant escalations waiting on a human, oldest first.</p>
      <AsyncBoundary
        state={state}
        errorMessage="Failed to load support tickets."
        emptyMessage="No open tickets — the assistant is handling everything."
        isEmpty={(rows) => rows.length === 0}
      >
        {(rows) => <Tickets initial={rows} />}
      </AsyncBoundary>
    </div>
  );
}

function Tickets({ initial }: { initial: SupportTicketDto[] }) {
  const [rows, setRows] = useState(initial);
  const [busy, setBusy] = useState(false);

  const resolve = async (ticketId: string) => {
    setBusy(true);
    try {
      await resolveSupportTicket(ticketId);
      setRows((rs) => rs.filter((t) => t.ticketId !== ticketId));
    } catch {
      // resolution didn't go through — keep the ticket visible
    } finally {
      setBusy(false);
    }
  };

  if (rows.length === 0) return <p className="text-muted">No open tickets — the assistant is handling everything.</p>;

  return (
    <div className="space-y-4">
      {rows.map((t) => (
        <Card key={t.ticketId} className="flex flex-col gap-4 p-5 sm:flex-row sm:items-center">
          <Avatar name={t.userName ?? 'User'} size={44} />
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2">
              <h3 className="font-semibold text-ink">{t.subject}</h3>
              <Badge tone="amber">Open</Badge>
            </div>
            <p className="mt-1 text-sm text-muted">{t.summary}</p>
            <p className="mt-2 text-xs text-muted">
              {t.userName ?? `User ${t.userId.slice(0, 8)}`}
              {t.userEmail && ` · ${t.userEmail}`} · {formatIsoDateFull(t.createdAt)}
            </p>
          </div>
          <Button size="sm" disabled={busy} onClick={() => resolve(t.ticketId)}>Mark resolved</Button>
        </Card>
      ))}
    </div>
  );
}
