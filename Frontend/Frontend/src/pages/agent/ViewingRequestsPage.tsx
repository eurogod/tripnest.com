import { useMemo, useState } from 'react';
import {
  getMyViewingRequests, updateViewingRequestStatus,
  type ViewingRequestDto, type ViewingRequestStatus,
} from '../../api/agentWorkspace';
import { getProperties } from '../../api/properties';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import Badge, { type BadgeTone } from '../../components/ui/Badge';
import { formatIsoDateFull } from '../../api/backend';
import { CalendarIcon } from '../../components/tenant/icons';

const STATUS_TONES: Record<string, BadgeTone> = {
  Pending: 'amber',
  Confirmed: 'green',
  Completed: 'gray',
  Cancelled: 'red',
};

interface Row extends ViewingRequestDto {
  propertyTitle: string;
}

export default function ViewingRequestsPage() {
  const state = useAsync(async (): Promise<Row[]> => {
    const [requests, properties] = await Promise.all([getMyViewingRequests(), getProperties()]);
    const titles = new Map(properties.map((p) => [p.id, p.title]));
    return requests.map((r) => ({
      ...r,
      propertyTitle: titles.get(r.propertyId) ?? `Property ${r.propertyId.slice(0, 8)}`,
    }));
  });

  return (
    <div>
      <h1 className="text-3xl font-bold tracking-tight text-ink">Viewing requests</h1>
      <p className="mt-1 mb-6 text-sm text-muted">Property viewings tenants have booked with you.</p>
      <AsyncBoundary
        state={state}
        errorMessage="Failed to load viewing requests."
        emptyMessage="No viewing requests yet — they appear here when a tenant books a viewing with you."
        isEmpty={(rows) => rows.length === 0}
      >
        {(rows) => <Requests initial={rows} />}
      </AsyncBoundary>
    </div>
  );
}

function Requests({ initial }: { initial: Row[] }) {
  const [rows, setRows] = useState(initial);
  const [busy, setBusy] = useState(false);
  const pending = useMemo(() => rows.filter((r) => r.status === 'Pending').length, [rows]);

  const setStatus = async (id: string, status: ViewingRequestStatus) => {
    setBusy(true);
    try {
      await updateViewingRequestStatus(id, status);
      setRows((rs) => rs.map((r) => (r.viewingRequestId === id ? { ...r, status } : r)));
    } catch {
      // status change didn't go through — leave the row untouched
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <p className="mb-4 text-sm text-muted">{pending} request{pending === 1 ? '' : 's'} awaiting your confirmation.</p>
      <div className="space-y-4">
        {rows.map((r) => (
          <Card key={r.viewingRequestId} className="flex flex-col gap-4 p-5 sm:flex-row sm:items-center">
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2">
                <h3 className="font-semibold text-ink">{r.propertyTitle}</h3>
                <Badge tone={STATUS_TONES[r.status] ?? 'gray'}>{r.status}</Badge>
              </div>
              <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-sm text-muted">
                <span className="flex items-center gap-1.5"><CalendarIcon size={14} /> {formatIsoDateFull(r.scheduledAt)}</span>
                <span>Tenant {r.tenantId.slice(0, 8)}</span>
              </div>
              {r.notes && <p className="mt-2 text-sm text-muted">“{r.notes}”</p>}
            </div>
            <div className="flex shrink-0 gap-2">
              {r.status === 'Pending' && (
                <>
                  <Button size="sm" disabled={busy} onClick={() => setStatus(r.viewingRequestId, 'Confirmed')}>Confirm</Button>
                  <Button size="sm" variant="ghost" className="text-rose-600 hover:bg-rose-50" disabled={busy} onClick={() => setStatus(r.viewingRequestId, 'Cancelled')}>Decline</Button>
                </>
              )}
              {r.status === 'Confirmed' && (
                <Button size="sm" variant="dark" disabled={busy} onClick={() => setStatus(r.viewingRequestId, 'Completed')}>Mark completed</Button>
              )}
            </div>
          </Card>
        ))}
      </div>
    </div>
  );
}
