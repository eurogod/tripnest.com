import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { agentsApi, dashboardApi } from '@/lib/services';
import { PageHeader, Async } from '@/components/dashboard';
import { Button } from '@/components/ui';
import { ServiceStatusPill } from '@/components/badges';
import { Calendar } from '@/components/icons';
import { fmtDateTime } from '@/lib/format';
import { usePropertyLookup } from '@/lib/hooks';
import { useAuth } from '@/auth/AuthContext';
import { useToast } from '@/components/Toast';

type Raw = Record<string, unknown>;
const str = (o: Raw, ...keys: string[]): string | undefined => {
  for (const k of keys) if (typeof o[k] === 'string') return o[k] as string;
  return undefined;
};
function viewingsOf(d: Raw | undefined): Raw[] {
  for (const k of ['viewingRequests', 'viewings', 'pendingViewings', 'requests']) {
    if (Array.isArray(d?.[k])) return d![k] as Raw[];
  }
  return [];
}

export default function AgentViewings() {
  const { user } = useAuth();
  const qc = useQueryClient();
  const toast = useToast();
  const query = useQuery({ queryKey: ['agent-dashboard'], queryFn: dashboardApi.agent, enabled: !!user });
  const props = usePropertyLookup('all');
  const [busy, setBusy] = useState<string | null>(null);

  async function update(id: string, status: string) {
    setBusy(id + status);
    try {
      await agentsApi.updateViewing(id, status);
      toast.success(`Viewing ${status.toLowerCase()}`);
      qc.invalidateQueries({ queryKey: ['agent-dashboard'] });
    } catch {
      toast.error('Could not update viewing');
    } finally {
      setBusy(null);
    }
  }

  return (
    <div>
      <PageHeader title="Viewing requests" subtitle="Confirm, complete or decline the visits tenants request." />
      <Async
        query={query}
        isEmpty={(d) => viewingsOf(d as Raw).length === 0}
        emptyIcon={<Calendar className="h-6 w-6" />}
        emptyTitle="No viewing requests"
        emptySubtitle="When a tenant asks to view a property you handle, it’ll show up here."
      >
        {(d) => {
          const rows = viewingsOf(d as Raw);
          return (
            <div className="space-y-3">
              {rows.map((r, i) => {
                const id = str(r, 'viewingRequestId', 'id') ?? String(i);
                const pid = str(r, 'propertyId');
                const status = str(r, 'status') ?? 'Pending';
                const isPending = status === 'Pending';
                return (
                  <div key={id} className="card flex flex-wrap items-center justify-between gap-3 p-4">
                    <div>
                      <p className="font-semibold">{str(r, 'propertyTitle', 'title') ?? (pid ? props.get(pid)?.title : undefined) ?? 'Property'}</p>
                      <p className="mt-0.5 text-sm text-muted">
                        <Calendar className="mr-1 inline h-3.5 w-3.5" />
                        {fmtDateTime(str(r, 'scheduledAt'))}
                      </p>
                      {str(r, 'notes') && <p className="mt-1 text-sm text-muted">“{str(r, 'notes')}”</p>}
                    </div>
                    <div className="flex items-center gap-2">
                      <ServiceStatusPill status={status} />
                      {isPending && (
                        <>
                          <Button size="sm" loading={busy === id + 'Confirmed'} onClick={() => update(id, 'Confirmed')}>
                            Confirm
                          </Button>
                          <Button variant="ghost" size="sm" loading={busy === id + 'Cancelled'} onClick={() => update(id, 'Cancelled')}>
                            Decline
                          </Button>
                        </>
                      )}
                      {status === 'Confirmed' && (
                        <Button variant="outline" size="sm" loading={busy === id + 'Completed'} onClick={() => update(id, 'Completed')}>
                          Mark complete
                        </Button>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          );
        }}
      </Async>
    </div>
  );
}
