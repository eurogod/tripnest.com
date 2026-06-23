import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { caretakersApi } from '@/lib/services';
import { PageHeader, Async } from '@/components/dashboard';
import { Button } from '@/components/ui';
import { ServiceStatusPill } from '@/components/badges';
import { Wrench } from '@/components/icons';
import { fmtDate } from '@/lib/format';
import { usePropertyLookup } from '@/lib/hooks';
import { useAuth } from '@/auth/AuthContext';
import { useToast } from '@/components/Toast';
import type { ServiceRequest } from '@/types/api';

export default function CaretakerRequests() {
  const { user } = useAuth();
  const qc = useQueryClient();
  const toast = useToast();
  const query = useQuery({ queryKey: ['service-requests'], queryFn: caretakersApi.myServiceRequests, enabled: !!user });
  const props = usePropertyLookup('all');
  const [busy, setBusy] = useState<string | null>(null);

  async function act(id: string, label: string, fn: () => Promise<unknown>) {
    setBusy(id + label);
    try {
      await fn();
      toast.success(label);
      qc.invalidateQueries({ queryKey: ['service-requests'] });
    } catch {
      toast.error('Could not update request');
    } finally {
      setBusy(null);
    }
  }

  function actions(r: ServiceRequest) {
    switch (r.status) {
      case 'Pending':
        return (
          <Button size="sm" loading={busy === r.serviceRequestId + 'Accepted'} onClick={() => act(r.serviceRequestId, 'Accepted', () => caretakersApi.acceptServiceRequest(r.serviceRequestId))}>
            Accept
          </Button>
        );
      case 'Accepted':
        return (
          <Button size="sm" loading={busy === r.serviceRequestId + 'Started'} onClick={() => act(r.serviceRequestId, 'Started', () => caretakersApi.updateServiceRequest(r.serviceRequestId, 'InProgress'))}>
            Start work
          </Button>
        );
      case 'InProgress':
        return (
          <Button size="sm" variant="outline" loading={busy === r.serviceRequestId + 'Completed'} onClick={() => act(r.serviceRequestId, 'Completed', () => caretakersApi.updateServiceRequest(r.serviceRequestId, 'Completed'))}>
            Mark complete
          </Button>
        );
      default:
        return null;
    }
  }

  return (
    <div>
      <PageHeader title="Service requests" subtitle="Jobs assigned to you — accept, start and complete them." />
      <Async
        query={query}
        emptyIcon={<Wrench className="h-6 w-6" />}
        emptyTitle="No service requests"
        emptySubtitle="When work is assigned to you it’ll appear here."
      >
        {(items) => (
          <div className="space-y-3">
            {items.map((r) => (
              <div key={r.serviceRequestId} className="card flex flex-wrap items-center justify-between gap-3 p-4">
                <div>
                  <p className="font-semibold">{r.serviceType}</p>
                  <p className="mt-0.5 text-sm text-muted">{r.description}</p>
                  <p className="mt-1 text-xs text-muted">
                    {props.get(r.propertyId)?.title ?? 'Property'} · {fmtDate(r.createdAt)}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <ServiceStatusPill status={r.status} />
                  {actions(r)}
                </div>
              </div>
            ))}
          </div>
        )}
      </Async>
    </div>
  );
}
