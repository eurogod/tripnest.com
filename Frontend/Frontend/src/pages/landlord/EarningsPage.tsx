import { useState } from 'react';
import type { EarningsSummary, EarningStatus, PayoutAccount } from '../../types';
import { getEarnings, getPayoutAccount, retryPayout, savePayoutAccount } from '../../api/earnings';
import { ApiError } from '../../api/client';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import Badge, { type BadgeTone } from '../../components/ui/Badge';
import { formatCedi } from '../../lib/format';
import { CardIcon, ClockIcon } from '../../components/tenant/icons';

const TXN_STATUS: Record<EarningStatus, { tone: BadgeTone; label: string }> = {
  settled: { tone: 'green', label: 'Paid' },
  pending: { tone: 'amber', label: 'Pending' },
  processing: { tone: 'blue', label: 'Processing' },
  failed: { tone: 'red', label: 'Failed' },
};

// Paystack transfer channels/providers for Ghana.
const PROVIDERS: { channel: 'mobile_money' | 'ghipss'; code: string; label: string }[] = [
  { channel: 'mobile_money', code: 'MTN', label: 'MTN MoMo' },
  { channel: 'mobile_money', code: 'ATL', label: 'AirtelTigo Money' },
  { channel: 'mobile_money', code: 'VOD', label: 'Telecel Cash' },
  { channel: 'ghipss', code: 'GHIPSS', label: 'Bank account (GhIPSS)' },
];

function Stat({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <Card className="p-4">
      <p className="text-xs text-muted">{label}</p>
      <p className="mt-1 text-2xl font-bold text-ink">{value}</p>
      {hint && <p className="text-xs text-muted">{hint}</p>}
    </Card>
  );
}

/** Registered payout destination + PUT /api/payouts/account form. */
function PayoutAccountCard() {
  const state = useAsync(getPayoutAccount, []);
  const [override, setOverride] = useState<PayoutAccount | null>(null);
  const [editing, setEditing] = useState(false);
  const [provider, setProvider] = useState(PROVIDERS[0]);
  const [accountNumber, setAccountNumber] = useState('');
  const [accountName, setAccountName] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const account = override ?? state.data ?? null;

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    if (saving) return;
    setSaving(true);
    setError('');
    try {
      const saved = await savePayoutAccount({
        channel: provider.channel,
        providerCode: provider.code,
        accountNumber: accountNumber.trim(),
        accountName: accountName.trim(),
      });
      setOverride(saved);
      setEditing(false);
      setAccountNumber('');
      setAccountName('');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not save that payout account.');
    } finally {
      setSaving(false);
    }
  };

  const providerLabel = (a: PayoutAccount) =>
    PROVIDERS.find((p) => p.code === a.providerCode)?.label ?? a.providerCode;

  return (
    <Card className="p-5">
      <div className="flex items-center gap-2">
        <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-gray-100 text-ink">
          <CardIcon size={18} />
        </span>
        <p className="text-sm font-semibold text-ink">Payout account</p>
      </div>

      {state.loading && !override ? (
        <p className="mt-3 text-sm text-muted">Checking your payout account…</p>
      ) : editing || !account ? (
        <form onSubmit={save} className="mt-3 space-y-2">
          {!account && (
            <p className="text-xs text-muted">
              Register where your payouts should be sent. The account is validated with Paystack.
            </p>
          )}
          <select
            value={provider.code}
            onChange={(e) => setProvider(PROVIDERS.find((p) => p.code === e.target.value) ?? PROVIDERS[0])}
            className="w-full rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm outline-none focus:border-brand"
          >
            {PROVIDERS.map((p) => (
              <option key={p.code} value={p.code}>{p.label}</option>
            ))}
          </select>
          <input
            value={accountNumber}
            onChange={(e) => setAccountNumber(e.target.value)}
            inputMode="numeric"
            placeholder={provider.channel === 'mobile_money' ? 'MoMo wallet number' : 'Bank account number'}
            className="w-full rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm outline-none focus:border-brand"
          />
          <input
            value={accountName}
            onChange={(e) => setAccountName(e.target.value)}
            placeholder="Account holder name"
            className="w-full rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm outline-none focus:border-brand"
          />
          {error && <p className="text-xs text-rose-600" role="alert">{error}</p>}
          <div className="flex gap-2">
            <Button type="submit" size="sm" className="flex-1" disabled={saving || accountNumber.trim().length < 8 || accountName.trim().length < 2}>
              {saving ? 'Saving…' : 'Save account'}
            </Button>
            {account && (
              <Button type="button" size="sm" variant="ghost" onClick={() => setEditing(false)}>Cancel</Button>
            )}
          </div>
        </form>
      ) : (
        <>
          <p className="mt-3 text-sm font-medium text-ink">
            {providerLabel(account)} · {account.accountNumber}
          </p>
          <p className="text-xs text-muted">{account.accountName}</p>
          <div className="mt-2">
            {account.providerRegistered ? (
              <Badge tone="green">Ready for transfers</Badge>
            ) : (
              <Badge tone="amber">Awaiting provider registration</Badge>
            )}
          </div>
          <Button variant="ghost" size="sm" className="mt-2 px-0 hover:bg-transparent" onClick={() => setEditing(true)}>
            Change account
          </Button>
        </>
      )}
    </Card>
  );
}

function Earnings({ data }: { data: EarningsSummary }) {
  const [rows, setRows] = useState(data.transactions);
  const [retryingId, setRetryingId] = useState<string | null>(null);
  const [retryError, setRetryError] = useState('');

  const retry = async (id: string) => {
    setRetryingId(id);
    setRetryError('');
    try {
      const updated = await retryPayout(id);
      setRows((rs) => rs.map((t) => (t.id === id ? { ...t, status: updated.status, date: updated.date } : t)));
    } catch (err) {
      setRetryError(err instanceof ApiError ? err.message : 'Could not retry that payout.');
    } finally {
      setRetryingId(null);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight text-ink">Earnings</h1>
        <p className="mt-1 text-muted">Track payouts and settled bookings across your portfolio.</p>
      </div>

      {retryError && (
        <div className="rounded-xl border border-rose-100 bg-rose-50 px-4 py-3 text-sm font-medium text-rose-600" role="alert">
          {retryError}
        </div>
      )}

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <Stat label="Lifetime earnings" value={formatCedi(data.lifetime)} />
        <Stat label="In-flight payouts" value={formatCedi(data.pending)} hint="Pending or processing" />
        <Stat label="This month" value={formatCedi(data.thisMonth)} />
        <Stat label="Last month" value={formatCedi(data.lastMonth)} />
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-[1fr_320px]">
        <Card className="min-w-0 overflow-x-auto">
          <div className="flex items-center justify-between px-5 py-4">
            <h2 className="font-bold text-ink">Payouts</h2>
          </div>
          {rows.length === 0 ? (
            <p className="px-5 pb-6 text-sm text-muted">
              No payouts yet — they appear here automatically when a stay's escrow is released.
            </p>
          ) : (
            <table className="w-full min-w-[640px] text-left">
              <thead>
                <tr className="border-y border-gray-100 text-xs font-semibold uppercase tracking-wide text-muted">
                  <th className="px-5 py-3 font-semibold">Booking</th>
                  <th className="px-5 py-3 font-semibold">Date</th>
                  <th className="px-5 py-3 font-semibold">Gross</th>
                  <th className="px-5 py-3 font-semibold">Fee</th>
                  <th className="px-5 py-3 font-semibold">Net</th>
                  <th className="px-5 py-3 font-semibold">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {rows.map((t) => (
                  <tr key={t.id}>
                    <td className="px-5 py-4">
                      <span className="font-medium text-ink">{t.guest}</span>
                      <span className="block text-xs text-muted">{t.listing}</span>
                    </td>
                    <td className="px-5 py-4 text-muted">{t.date}</td>
                    <td className="px-5 py-4 text-muted">{formatCedi(t.gross)}</td>
                    <td className="px-5 py-4 text-muted">−{formatCedi(t.fee)}</td>
                    <td className="px-5 py-4 font-semibold text-ink">{formatCedi(t.net)}</td>
                    <td className="px-5 py-4">
                      <span className="flex items-center gap-2">
                        <Badge tone={TXN_STATUS[t.status].tone}>{TXN_STATUS[t.status].label}</Badge>
                        {(t.status === 'failed' || t.status === 'pending') && (
                          <button
                            type="button"
                            onClick={() => void retry(t.id)}
                            disabled={retryingId === t.id}
                            className="text-xs font-semibold text-brand hover:underline disabled:opacity-50"
                          >
                            {retryingId === t.id ? 'Retrying…' : 'Retry'}
                          </button>
                        )}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Card>

        <aside className="space-y-5">
          <Card className="border-ink! bg-ink! p-5 text-white">
            <p className="text-sm text-white/70">Lifetime earnings</p>
            <p className="mt-1 text-3xl font-bold">{formatCedi(data.lifetime)}</p>
            <p className="mt-3 text-xs text-white/70">
              Payouts are sent automatically to your registered account when a stay's
              escrow is released — no manual withdrawal needed.
            </p>
          </Card>

          <Card className="p-5">
            <div className="flex items-center gap-2">
              <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-gray-100 text-ink">
                <ClockIcon size={18} />
              </span>
              <div>
                <p className="text-sm font-semibold text-ink">In-flight payouts</p>
                <p className="text-xs text-muted">{data.nextPayoutDate}</p>
              </div>
            </div>
            <p className="mt-3 text-2xl font-bold text-ink">{formatCedi(data.pending)}</p>
            <p className="text-xs text-muted">Clears once the provider confirms the transfer.</p>
          </Card>

          <PayoutAccountCard />
        </aside>
      </div>
    </div>
  );
}

export default function EarningsPage() {
  const state = useAsync(getEarnings, []);
  return (
    <AsyncBoundary state={state} loadingMessage="Loading earnings…" errorMessage="Failed to load earnings.">
      {(data) => <Earnings data={data} />}
    </AsyncBoundary>
  );
}
