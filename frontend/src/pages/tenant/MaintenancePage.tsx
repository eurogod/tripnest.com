import { useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { bookingsApi, maintenanceApi } from '@/lib/services';
import { PageHeader, Async } from '@/components/dashboard';
import { Button, Field } from '@/components/ui';
import { Modal } from '@/components/Modal';
import { MaintenanceStatusPill } from '@/components/badges';
import { Wrench, Plus } from '@/components/icons';
import { fmtDate } from '@/lib/format';
import { useAppConfig, usePropertyLookup } from '@/lib/hooks';
import { useAuth } from '@/auth/AuthContext';
import { useToast } from '@/components/Toast';

const PRIORITIES = ['Low', 'Medium', 'High', 'Urgent'];

export default function MaintenancePage() {
  const { user } = useAuth();
  const [open, setOpen] = useState(false);

  const query = useQuery({ queryKey: ['maintenance-mine'], queryFn: maintenanceApi.mine, enabled: !!user });
  const props = usePropertyLookup('all');

  return (
    <div>
      <PageHeader
        title="Maintenance"
        subtitle="Report issues at your stay and track them to resolution."
        action={
          <Button onClick={() => setOpen(true)}>
            <Plus className="h-4 w-4" /> Report issue
          </Button>
        }
      />
      <Async
        query={query}
        emptyIcon={<Wrench className="h-6 w-6" />}
        emptyTitle="No maintenance requests"
        emptySubtitle="Report a broken tap, faulty wiring or anything that needs fixing."
        emptyAction={<Button onClick={() => setOpen(true)}>Report issue</Button>}
      >
        {(items) => (
          <div className="space-y-3">
            {items.map((m) => (
              <div key={m.maintenanceId} className="card p-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="font-semibold">{props.get(m.propertyId)?.title ?? 'Property'}</p>
                    <p className="mt-0.5 text-sm text-muted">{m.description}</p>
                    <p className="mt-1 text-xs text-muted">Reported {fmtDate(m.createdAt)}</p>
                  </div>
                  <MaintenanceStatusPill status={m.status} />
                </div>
                {m.resolution && <p className="mt-2 rounded-lg bg-success/5 p-2 text-sm text-success">{m.resolution}</p>}
              </div>
            ))}
          </div>
        )}
      </Async>

      <ReportModal open={open} onClose={() => setOpen(false)} />
    </div>
  );
}

function ReportModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { user } = useAuth();
  const qc = useQueryClient();
  const toast = useToast();
  const config = useAppConfig();
  const props = usePropertyLookup('all');
  const bookings = useQuery({ queryKey: ['my-bookings'], queryFn: bookingsApi.mine, enabled: !!user && open });
  const [form, setForm] = useState({ propertyId: '', category: '', description: '', priority: 'Medium' });
  const [busy, setBusy] = useState(false);

  const categories = config.data?.maintenanceCategories ?? ['Plumbing', 'Electrical', 'Appliance', 'Structural', 'Other'];
  const myProperties = useMemo(() => {
    const ids = [...new Set((bookings.data ?? []).map((b) => b.propertyId))];
    return ids.map((id) => ({ id, title: props.get(id)?.title ?? 'Property' }));
  }, [bookings.data, props]);

  async function submit() {
    setBusy(true);
    try {
      await maintenanceApi.report({
        propertyId: form.propertyId,
        category: form.category || categories[0],
        description: form.description,
        priority: form.priority,
      });
      toast.success('Issue reported');
      qc.invalidateQueries({ queryKey: ['maintenance-mine'] });
      onClose();
      setForm({ propertyId: '', category: '', description: '', priority: 'Medium' });
    } catch {
      toast.error('Could not submit the report');
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal open={open} onClose={onClose} title="Report a maintenance issue" maxWidth="max-w-md">
      <div className="space-y-4">
        <Field label="Property">
          <select className="input" value={form.propertyId} onChange={(e) => setForm({ ...form, propertyId: e.target.value })}>
            <option value="">Select a property…</option>
            {myProperties.map((p) => (
              <option key={p.id} value={p.id}>
                {p.title}
              </option>
            ))}
          </select>
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Category">
            <select className="input" value={form.category} onChange={(e) => setForm({ ...form, category: e.target.value })}>
              {categories.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
          </Field>
          <Field label="Priority">
            <select className="input" value={form.priority} onChange={(e) => setForm({ ...form, priority: e.target.value })}>
              {PRIORITIES.map((p) => (
                <option key={p} value={p}>
                  {p}
                </option>
              ))}
            </select>
          </Field>
        </div>
        <Field label="What's wrong?">
          <textarea
            className="input min-h-[96px] resize-y"
            placeholder="Describe the issue in a sentence or two…"
            value={form.description}
            onChange={(e) => setForm({ ...form, description: e.target.value })}
          />
        </Field>
        <Button block loading={busy} disabled={!form.propertyId || !form.description} onClick={submit}>
          Submit report
        </Button>
      </div>
    </Modal>
  );
}
