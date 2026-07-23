import { useMemo, useState } from 'react';
import {
  getMyServiceRequests, acceptServiceRequest, updateServiceRequestStatus,
  type ServiceRequestDto,
} from '../../api/caretakerWorkspace';
import { getProperties } from '../../api/properties';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import Badge, { type BadgeTone } from '../../components/ui/Badge';
import { formatIsoDate } from '../../api/backend';
import { StarIcon } from '../../components/tenant/icons';

const STATUS: Record<string, { tone: BadgeTone; label: string }> = {
  Pending: { tone: 'amber', label: 'Pending' },
  Accepted: { tone: 'blue', label: 'Accepted' },
  InProgress: { tone: 'blue', label: 'In progress' },
  Completed: { tone: 'green', label: 'Completed' },
  Cancelled: { tone: 'red', label: 'Cancelled' },
};

const TABS = ['All', 'Pending', 'Active', 'Completed'] as const;
type Tab = (typeof TABS)[number];

interface Row extends ServiceRequestDto {
  propertyTitle: string;
}

export default function ServiceRequestsPage() {
  const state = useAsync(async (): Promise<Row[]> => {
    const [requests, properties] = await Promise.all([getMyServiceRequests(), getProperties()]);
    const titles = new Map(properties.map((p) => [p.id, p.title]));
    return requests.map((r) => ({
      ...r,
      propertyTitle: titles.get(r.propertyId) ?? `Property ${r.propertyId.slice(0, 8)}`,
    }));
  });

  return (
    <div>
      <h1 className="text-3xl font-bold tracking-tight text-ink">Service requests</h1>
      <p className="mt-1 mb-6 text-sm text-muted">Work raised against your property assignments.</p>
      <AsyncBoundary
        state={state}
        errorMessage="Failed to load service requests."
        emptyMessage="No service requests yet — they appear here once a landlord assigns you to a property."
        isEmpty={(rows) => rows.length === 0}
      >
        {(rows) => <Requests initial={rows} />}
      </AsyncBoundary>
    </div>
  );
}

function Requests({ initial }: { initial: Row[] }) {
  const [rows, setRows] = useState(initial);
  const [tab, setTab] = useState<Tab>('All');
  const [busy, setBusy] = useState(false);

  const visible = useMemo(() => {
    switch (tab) {
      case 'Pending': return rows.filter((r) => r.status === 'Pending');
      case 'Active': return rows.filter((r) => r.status === 'Accepted' || r.status === 'InProgress');
      case 'Completed': return rows.filter((r) => r.status === 'Completed');
      default: return rows;
    }
  }, [rows, tab]);

  const mutate = async (id: string, action: () => Promise<unknown>, status: string) => {
    setBusy(true);
    try {
      await action();
      setRows((rs) => rs.map((r) => (r.serviceRequestId === id ? { ...r, status } : r)));
    } catch {
      // change didn't go through — leave the row untouched
    } finally {
      setBusy(false);
    }
  };

  const accept = (id: string) => mutate(id, () => acceptServiceRequest(id), 'Accepted');
  const start = (id: string) => mutate(id, () => updateServiceRequestStatus(id, 'InProgress'), 'InProgress');
  const complete = (id: string) => mutate(id, () => updateServiceRequestStatus(id, 'Completed'), 'Completed');

  return (
    <div>
      <div className="mb-6 flex flex-wrap gap-2">
        {TABS.map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`rounded-full border px-3.5 py-1.5 text-sm font-medium transition-colors ${
              tab === t ? 'border-brand bg-brand-50 text-brand' : 'border-gray-200 text-gray-600 hover:bg-gray-100'
            }`}
          >
            {t}
          </button>
        ))}
      </div>

      {visible.length === 0 ? (
        <p className="text-muted">No {tab === 'All' ? '' : tab.toLowerCase()} requests.</p>
      ) : (
        <div className="space-y-4">
          {visible.map((r) => {
            const s = STATUS[r.status] ?? { tone: 'gray' as BadgeTone, label: r.status };
            return (
              <Card key={r.serviceRequestId} className="flex flex-col gap-4 p-5 sm:flex-row sm:items-center">
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2">
                    <h3 className="font-semibold text-ink">{r.serviceType}</h3>
                    <Badge tone={s.tone}>{s.label}</Badge>
                  </div>
                  <p className="mt-1 text-sm text-muted">{r.description}</p>
                  <p className="mt-2 text-xs text-muted">
                    {r.propertyTitle} · raised {formatIsoDate(r.createdAt)}
                    {r.completedAt && ` · completed ${formatIsoDate(r.completedAt)}`}
                  </p>
                  {r.rating != null && (
                    <p className="mt-2 flex items-center gap-1.5 text-sm text-ink">
                      <StarIcon size={14} className="text-amber-500" /> {r.rating}/5
                      {r.reviewComment && <span className="text-muted">— “{r.reviewComment}”</span>}
                    </p>
                  )}
                </div>
                <div className="flex shrink-0 gap-2">
                  {r.status === 'Pending' && (
                    <Button size="sm" disabled={busy} onClick={() => accept(r.serviceRequestId)}>Accept</Button>
                  )}
                  {r.status === 'Accepted' && (
                    <Button size="sm" disabled={busy} onClick={() => start(r.serviceRequestId)}>Start work</Button>
                  )}
                  {(r.status === 'Accepted' || r.status === 'InProgress') && (
                    <Button size="sm" variant="dark" disabled={busy} onClick={() => complete(r.serviceRequestId)}>Mark completed</Button>
                  )}
                </div>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}
