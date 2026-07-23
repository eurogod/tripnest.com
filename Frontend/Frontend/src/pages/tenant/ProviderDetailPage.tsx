import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import type { Property, ServiceProvider } from '../../types';
import { getProviderById, requestAgentViewing, requestCaretakerService } from '../../api/services';
import { getProperties } from '../../api/properties';
import { startConversation } from '../../api/messages';
import { rememberContact } from '../../lib/chatContacts';
import { useSession } from '../../store/authStore';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import Badge from '../../components/ui/Badge';
import Avatar from '../../components/ui/Avatar';
import { formatCedi } from '../../lib/format';
import { MapPinIcon, MessageIcon, ShieldIcon, StarIcon } from '../../components/tenant/icons';

const inputCls =
  'w-full rounded-xl border border-gray-300 px-4 py-2.5 text-sm text-ink outline-none transition-colors focus:border-brand';

const CATEGORY_ROLE: Record<string, string> = {
  Agents: 'Agent',
  Caretakers: 'Caretaker',
  'House Help': 'House help',
};

/** "Message now" follow-up shown once a request has been sent. Live providers only. */
function ChatNowButton({ provider }: { provider: ServiceProvider }) {
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!provider.userId) return null;

  const open = async () => {
    setBusy(true);
    setError(null);
    try {
      rememberContact(provider.userId!, provider.name, CATEGORY_ROLE[provider.category]);
      const conversationId = await startConversation(provider.userId!);
      navigate(`/messages/${conversationId}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not open a conversation.');
      setBusy(false);
    }
  };

  return (
    <div className="mt-3">
      <button
        type="button"
        onClick={() => void open()}
        disabled={busy}
        className="flex w-full items-center justify-center gap-2 rounded-lg bg-brand py-2 text-sm font-semibold text-white transition-colors hover:bg-brand/90 disabled:opacity-60"
      >
        <MessageIcon size={15} /> {busy ? 'Opening chat…' : 'Message now'}
      </button>
      {error && <p className="mt-1.5 text-xs text-rose-600">{error}</p>}
    </div>
  );
}

function RequestServiceForm({ provider }: { provider: ServiceProvider }) {
  const navigate = useNavigate();
  const session = useSession();
  const [serviceType, setServiceType] = useState(provider.skills[0] ?? 'General');
  const [description, setDescription] = useState('');
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!session) { navigate('/welcome'); return; }
    if (!description.trim()) { setError('Describe what you need help with.'); return; }
    setBusy(true);
    setError(null);
    try {
      if (provider.userId) {
        await requestCaretakerService({
          caretakerId: provider.id,
          serviceType,
          description: description.trim(),
        });
      }
      setDone(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not send the request.');
    } finally {
      setBusy(false);
    }
  };

  if (done) {
    return (
      <div className="rounded-xl bg-brand-50 p-4 text-sm text-brand">
        <p className="font-semibold">Request sent ✓</p>
        <p className="mt-1">
          {provider.name} has been notified and can message you to arrange the details.
        </p>
        <ChatNowButton provider={provider} />
      </div>
    );
  }

  return (
    <form onSubmit={submit} className="space-y-3">
      <div>
        <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-gray-400">Service</label>
        <select value={serviceType} onChange={(e) => setServiceType(e.target.value)} className={inputCls}>
          {provider.skills.map((s) => <option key={s}>{s}</option>)}
          <option>Other</option>
        </select>
      </div>
      <div>
        <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-gray-400">What do you need?</label>
        <textarea
          value={description}
          onChange={(e) => { setDescription(e.target.value); setError(null); }}
          rows={3}
          placeholder="e.g. Deep-clean a 2-bedroom apartment this Saturday morning"
          className={inputCls}
        />
        {error && <p className="mt-1 text-xs text-rose-600">{error}</p>}
      </div>
      <Button type="submit" disabled={busy} className="w-full">
        {busy ? 'Sending…' : session ? 'Send request' : 'Sign in to request'}
      </Button>
    </form>
  );
}

function RequestViewingForm({ provider }: { provider: ServiceProvider }) {
  const navigate = useNavigate();
  const session = useSession();
  const properties = useAsync(getProperties, []);
  const [propertyId, setPropertyId] = useState('');
  const [when, setWhen] = useState('');
  const [notes, setNotes] = useState('');
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (e: React.FormEvent, rows: Property[]) => {
    e.preventDefault();
    if (!session) { navigate('/welcome'); return; }
    const chosen = propertyId || rows[0]?.id;
    if (!chosen || !when) { setError('Pick a property and a date for the viewing.'); return; }
    setBusy(true);
    setError(null);
    try {
      if (provider.userId) {
        await requestAgentViewing(provider.id, {
          propertyId: chosen,
          scheduledAt: new Date(when).toISOString(),
          notes: notes.trim() || undefined,
        });
      }
      setDone(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not send the request.');
    } finally {
      setBusy(false);
    }
  };

  if (done) {
    return (
      <div className="rounded-xl bg-brand-50 p-4 text-sm text-brand">
        <p className="font-semibold">Viewing requested ✓</p>
        <p className="mt-1">The agent will confirm the time and can message you here on TripNest.</p>
        <ChatNowButton provider={provider} />
      </div>
    );
  }

  return (
    <AsyncBoundary state={properties} loadingMessage="Loading properties…" errorMessage="Failed to load properties.">
      {(rows) => (
        <form onSubmit={(e) => submit(e, rows)} className="space-y-3">
          <div>
            <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-gray-400">Property to view</label>
            <select value={propertyId || rows[0]?.id || ''} onChange={(e) => setPropertyId(e.target.value)} className={inputCls}>
              {rows.map((p) => <option key={p.id} value={p.id}>{p.title}</option>)}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-gray-400">Preferred date & time</label>
            <input
              type="datetime-local"
              value={when}
              onChange={(e) => { setWhen(e.target.value); setError(null); }}
              className={inputCls}
            />
          </div>
          <div>
            <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-gray-400">Notes (optional)</label>
            <input
              type="text"
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Anything the agent should know"
              className={inputCls}
            />
          </div>
          {error && <p className="text-xs text-rose-600">{error}</p>}
          <Button type="submit" disabled={busy} className="w-full">
            {busy ? 'Sending…' : session ? 'Request viewing' : 'Sign in to request'}
          </Button>
        </form>
      )}
    </AsyncBoundary>
  );
}

function Detail({ provider }: { provider: ServiceProvider }) {
  const navigate = useNavigate();
  const session = useSession();
  const [chatBusy, setChatBusy] = useState(false);
  const [chatError, setChatError] = useState<string | null>(null);

  const message = async () => {
    if (!session) { navigate('/welcome'); return; }
    if (!provider.userId) { navigate('/messages'); return; }
    setChatBusy(true);
    setChatError(null);
    try {
      rememberContact(provider.userId, provider.name, CATEGORY_ROLE[provider.category]);
      const conversationId = await startConversation(provider.userId);
      navigate(`/messages/${conversationId}`);
    } catch (err) {
      setChatError(err instanceof Error ? err.message : 'Could not open a conversation.');
      setChatBusy(false);
    }
  };

  return (
    <div className="mx-auto max-w-4xl">
      <button onClick={() => navigate(-1)} className="mb-4 text-sm font-semibold text-muted hover:text-ink">
        ← Back
      </button>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-[1fr_360px]">
        <div className="space-y-5">
          <Card className="p-6">
            <div className="flex items-start gap-4">
              <Avatar name={provider.name} size={64} />
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-center gap-2">
                  <h1 className="text-2xl font-bold tracking-tight text-ink">{provider.name}</h1>
                  {provider.verified && (
                    <Badge tone="green">
                      <span className="inline-flex items-center gap-1"><ShieldIcon size={11} /> Verified</span>
                    </Badge>
                  )}
                </div>
                <p className="mt-0.5 text-muted">{provider.role}</p>
                <p className="mt-2 flex items-center gap-1.5 text-sm text-muted">
                  <MapPinIcon size={14} /> {provider.location}
                  {provider.reviews > 0 && (
                    <>
                      <span className="mx-1">·</span>
                      <StarIcon size={13} className="text-amber-400" />
                      <span className="font-semibold text-ink">{provider.rating}</span>
                      <span>({provider.reviews} reviews)</span>
                    </>
                  )}
                </p>
              </div>
            </div>

            {provider.bio && <p className="mt-4 text-sm leading-relaxed text-gray-600">{provider.bio}</p>}

            <div className="mt-4 flex flex-wrap gap-1.5">
              {provider.skills.map((s) => (
                <span key={s} className="rounded-full bg-gray-100 px-3 py-1 text-xs text-gray-600">{s}</span>
              ))}
            </div>

            <div className="mt-5 flex items-center justify-between border-t border-gray-100 pt-4">
              <span className="text-sm">
                {provider.ratePeriod === 'commission' ? (
                  <span className="text-muted">Commission based</span>
                ) : (
                  <>
                    <span className="text-lg font-bold text-brand">{formatCedi(provider.rate)}</span>
                    <span className="text-muted"> / {provider.ratePeriod}</span>
                  </>
                )}
              </span>
              <Button variant="ghost" size="sm" className="gap-1.5" onClick={message} disabled={chatBusy}>
                <MessageIcon size={15} /> {chatBusy ? 'Opening chat…' : 'Message'}
              </Button>
            </div>
            {chatError && <p className="mt-2 text-xs text-rose-600">{chatError}</p>}
          </Card>
        </div>

        <aside>
          <Card className="p-5">
            <h2 className="mb-3 font-bold text-ink">
              {provider.category === 'Agents' ? 'Request a viewing' : 'Request this service'}
            </h2>
            {provider.category === 'Agents'
              ? <RequestViewingForm provider={provider} />
              : <RequestServiceForm provider={provider} />}
            <p className="mt-3 text-[11px] leading-relaxed text-muted">
              After you send a request, you and {provider.category === 'Agents' ? 'the agent' : 'the caretaker'} can
              chat in Messages to agree on details.
            </p>
          </Card>
        </aside>
      </div>
    </div>
  );
}

export default function ProviderDetailPage() {
  const { id } = useParams<{ id: string }>();
  const state = useAsync(() => getProviderById(id ?? ''), [id]);

  return (
    <AsyncBoundary
      state={state}
      loadingMessage="Loading profile…"
      errorMessage="Failed to load this profile."
      emptyMessage="Provider not found."
      isEmpty={(p) => !p}
    >
      {(provider) => <Detail provider={provider!} />}
    </AsyncBoundary>
  );
}
