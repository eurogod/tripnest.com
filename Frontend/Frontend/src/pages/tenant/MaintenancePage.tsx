import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import type { MaintenanceStatus, MaintenanceTicket } from '../../types';
import { getMaintenanceTickets, createMaintenanceTicket } from '../../api/maintenance';
import { getCaretakersByProperty } from '../../api/services';
import { startConversation } from '../../api/messages';
import { rememberContact } from '../../lib/chatContacts';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import Badge, { type BadgeTone } from '../../components/ui/Badge';
import { ChatIcon } from '../../components/tenant/icons';

const STATUS: Record<MaintenanceStatus, { tone: BadgeTone; label: string }> = {
  pending: { tone: 'amber', label: 'Pending' },
  'in-progress': { tone: 'blue', label: 'In Progress' },
  resolved: { tone: 'green', label: 'Resolved' },
};

const CATEGORIES = ['Plumbing', 'Electrical', 'General'];

function MaintenanceView({ initial }: { initial: MaintenanceTicket[] }) {
  const navigate = useNavigate();
  const [tickets, setTickets] = useState(initial);
  const [title, setTitle] = useState('');
  const [category, setCategory] = useState(CATEGORIES[0]);
  const [saving, setSaving] = useState(false);
  const [submitted, setSubmitted] = useState<MaintenanceTicket | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [chatBusyId, setChatBusyId] = useState<string | number | null>(null);
  // Which caretaker (userId) looks after each property — powers the Chat actions.
  const caretakers = useAsync(getCaretakersByProperty, []);

  const counts = {
    pending: tickets.filter((t) => t.status === 'pending').length,
    'in-progress': tickets.filter((t) => t.status === 'in-progress').length,
    resolved: tickets.filter((t) => t.status === 'resolved').length,
  };

  const caretakerFor = (t: MaintenanceTicket): string | undefined =>
    t.propertyId ? caretakers.data?.[t.propertyId] : undefined;

  const openChat = async (t: MaintenanceTicket) => {
    const userId = caretakerFor(t);
    if (!userId) return;
    setChatBusyId(t.id);
    setError(null);
    try {
      rememberContact(userId, 'Property Caretaker', 'Caretaker');
      const conversationId = await startConversation(userId, t.propertyId);
      navigate(`/messages/${conversationId}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not open a conversation.');
      setChatBusyId(null);
    }
  };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim()) return;
    setSaving(true);
    setError(null);
    try {
      const ticket = await createMaintenanceTicket({ title: title.trim(), category });
      setTickets((t) => [ticket, ...t]);
      setSubmitted(ticket);
      setTitle('');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not submit the request.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="grid grid-cols-1 gap-6 lg:grid-cols-[1fr_320px]">
      <div className="min-w-0 space-y-6">
        <div className="grid grid-cols-3 gap-3">
          <Card className="p-4 text-center">
            <p className="text-2xl font-bold text-amber-600">{counts.pending}</p>
            <p className="text-xs text-muted">Pending</p>
          </Card>
          <Card className="p-4 text-center">
            <p className="text-2xl font-bold text-blue-600">{counts['in-progress']}</p>
            <p className="text-xs text-muted">In Progress</p>
          </Card>
          <Card className="p-4 text-center">
            <p className="text-2xl font-bold text-brand">{counts.resolved}</p>
            <p className="text-xs text-muted">Resolved</p>
          </Card>
        </div>

        <Card className="divide-y divide-gray-100 overflow-hidden">
          {tickets.map((t) => (
            <div key={t.id} className="flex items-center justify-between gap-4 px-5 py-4">
              <div className="min-w-0">
                <p className="truncate font-semibold text-ink">{t.title}</p>
                <p className="text-sm text-muted">{t.category} · Reported {t.reportedOn}</p>
              </div>
              <div className="flex shrink-0 items-center gap-2">
                {caretakerFor(t) && (
                  <button
                    type="button"
                    onClick={() => void openChat(t)}
                    disabled={chatBusyId === t.id}
                    className="flex items-center gap-1.5 rounded-full bg-brand-50 px-3 py-1.5 text-xs font-semibold text-brand transition-colors hover:bg-brand-50/70 disabled:opacity-60"
                  >
                    <ChatIcon size={13} /> {chatBusyId === t.id ? 'Opening…' : 'Chat'}
                  </button>
                )}
                <Badge tone={STATUS[t.status].tone}>{STATUS[t.status].label}</Badge>
              </div>
            </div>
          ))}
        </Card>
      </div>

      <Card className="h-fit p-5">
        <h2 className="mb-4 text-lg font-bold text-ink">Report an issue</h2>
        <form onSubmit={submit} className="space-y-3">
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">Issue</span>
            <input
              value={title}
              onChange={(e) => { setTitle(e.target.value); setSubmitted(null); }}
              placeholder="e.g. Leaking tap in kitchen"
              className="w-full rounded-lg border border-gray-200 px-3 py-2.5 text-sm text-ink outline-none focus:border-brand"
            />
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">Category</span>
            <select
              value={category}
              onChange={(e) => setCategory(e.target.value)}
              className="w-full rounded-lg border border-gray-200 px-3 py-2.5 text-sm text-ink outline-none focus:border-brand"
            >
              {CATEGORIES.map((c) => (
                <option key={c} value={c}>{c}</option>
              ))}
            </select>
          </label>
          <Button type="submit" disabled={saving} className="w-full">
            {saving ? 'Submitting…' : 'Submit request'}
          </Button>
          {error && <p className="text-xs text-rose-600">{error}</p>}
        </form>

        {submitted && (
          <div className="mt-4 rounded-xl bg-brand-50 p-4 text-sm text-brand">
            <p className="font-semibold">Request submitted ✓</p>
            <p className="mt-1">
              {caretakerFor(submitted)
                ? 'Your caretaker has been notified — you can chat to agree on the details.'
                : 'Your request has been logged and will be assigned shortly.'}
            </p>
            {caretakerFor(submitted) && (
              <button
                type="button"
                onClick={() => void openChat(submitted)}
                disabled={chatBusyId === submitted.id}
                className="mt-3 flex w-full items-center justify-center gap-2 rounded-lg bg-brand py-2 text-sm font-semibold text-white transition-colors hover:bg-brand/90 disabled:opacity-60"
              >
                <ChatIcon size={15} /> {chatBusyId === submitted.id ? 'Opening chat…' : 'Chat with your caretaker'}
              </button>
            )}
          </div>
        )}
      </Card>
    </div>
  );
}

export default function MaintenancePage() {
  const state = useAsync(getMaintenanceTickets, []);

  return (
    <div>
      <h1 className="mb-6 text-3xl font-bold text-ink">Maintenance</h1>
      <AsyncBoundary
        state={state}
        loadingMessage="Loading maintenance…"
        errorMessage="Failed to load maintenance."
      >
        {(rows) => <MaintenanceView initial={rows} />}
      </AsyncBoundary>
    </div>
  );
}
