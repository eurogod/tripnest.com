import { useEffect, useMemo, useState } from 'react';
import type { Payment, PaymentChannel, PaymentMethod, PaymentStatus } from '../../types';
import {
  addPaymentMethod,
  deletePaymentMethod,
  downloadReceipt,
  getPayments,
  getPaymentMethods,
  initiatePayment,
  setPrimaryPaymentMethod,
  verifyPayment,
} from '../../api/payments';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import RentAndSharesSection from '../../components/tenant/RentAndSharesSection';
import TenantClaimsSection from '../../components/tenant/TenantClaimsSection';
import Button from '../../components/ui/Button';
import Badge, { type BadgeTone } from '../../components/ui/Badge';
import { formatCedi } from '../../lib/format';
import { getSession } from '../../store/authStore';
import {
  CalendarIcon,
  CardIcon,
  CheckIcon,
  ClockIcon,
  PlusIcon,
  ShieldIcon,
} from '../../components/tenant/icons';

const STATUS: Record<PaymentStatus, { tone: BadgeTone; label: string }> = {
  paid: { tone: 'green', label: 'Paid' },
  due: { tone: 'amber', label: 'Due' },
  upcoming: { tone: 'gray', label: 'Upcoming' },
};

/** Toned icon chip shown at the left of each history row. */
const STATUS_CHIP: Record<PaymentStatus, string> = {
  paid: 'bg-brand-50 text-brand',
  due: 'bg-amber-50 text-amber-600',
  upcoming: 'bg-gray-100 text-gray-500',
};

const FILTERS: { id: 'all' | PaymentStatus; label: string }[] = [
  { id: 'all', label: 'All' },
  { id: 'due', label: 'Due' },
  { id: 'upcoming', label: 'Upcoming' },
  { id: 'paid', label: 'Paid' },
];

/** Brand accent for a payment method's provider chip. */
function providerAccent(provider: string): string {
  if (/mtn/i.test(provider)) return 'bg-amber-400 text-amber-950';
  if (/telecel|vodafone/i.test(provider)) return 'bg-rose-500 text-white';
  if (/airtel|tigo/i.test(provider)) return 'bg-blue-600 text-white';
  return 'bg-ink text-white';
}

function providerInitials(provider: string): string {
  return provider
    .split(/\s+/)
    .map((w) => w[0])
    .join('')
    .slice(0, 3)
    .toUpperCase();
}

/** Infer the provider channel so the charge routes correctly. */
function channelFor(method: PaymentMethod | undefined): PaymentChannel {
  return method && /card|visa|master/i.test(method.provider) ? 'card' : 'momo';
}

function StatCard({
  label,
  value,
  hint,
  icon,
  chip,
}: {
  label: string;
  value: string;
  hint: string;
  icon: React.ReactNode;
  chip: string;
}) {
  return (
    <Card className="flex items-center gap-4 p-5">
      <span className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-xl ${chip}`}>
        {icon}
      </span>
      <span className="min-w-0">
        <span className="block text-xs font-semibold uppercase tracking-wide text-muted">{label}</span>
        <span className="block truncate text-xl font-bold text-ink">{value}</span>
        <span className="block text-xs text-muted">{hint}</span>
      </span>
    </Card>
  );
}

function AddMethodForm({ onAdd }: { onAdd: (provider: string, digits: string) => Promise<void> }) {
  const [open, setOpen] = useState(false);
  const [provider, setProvider] = useState('MTN MoMo');
  const [number, setNumber] = useState('');
  const [saving, setSaving] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    const digits = number.replace(/\D/g, '');
    if (digits.length < 4 || saving) return;
    setSaving(true);
    try {
      await onAdd(provider, digits);
      setNumber('');
      setOpen(false);
    } finally {
      setSaving(false);
    }
  };

  if (!open) {
    return (
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="mt-1 flex w-full items-center justify-center gap-1.5 rounded-xl border border-dashed border-gray-300 px-3 py-3 text-sm font-semibold text-muted transition-colors hover:border-brand hover:text-brand"
      >
        <PlusIcon size={16} /> Add payment method
      </button>
    );
  }

  return (
    <form onSubmit={submit} className="mt-2 space-y-2 rounded-xl border border-gray-200 bg-gray-50 p-3">
      <select
        value={provider}
        onChange={(e) => setProvider(e.target.value)}
        className="w-full rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm text-ink outline-none focus:border-brand"
      >
        <option>MTN MoMo</option>
        <option>Telecel Cash</option>
        <option>AirtelTigo Money</option>
        <option>Debit Card</option>
      </select>
      <input
        value={number}
        onChange={(e) => setNumber(e.target.value)}
        inputMode="numeric"
        placeholder="Phone or card number"
        className="w-full rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm text-ink outline-none focus:border-brand"
      />
      <div className="flex gap-2">
        <Button type="submit" size="sm" className="flex-1" disabled={saving}>{saving ? 'Saving…' : 'Save'}</Button>
        <Button type="button" variant="ghost" size="sm" onClick={() => setOpen(false)}>Cancel</Button>
      </div>
    </form>
  );
}

function MethodRow({
  method,
  onSetPrimary,
  onRemove,
}: {
  method: PaymentMethod;
  onSetPrimary: (id: string) => void;
  onRemove: (id: string) => void;
}) {
  return (
    <div
      className={`flex items-center gap-3 rounded-xl border px-3 py-3 transition-colors ${
        method.primary ? 'border-brand bg-brand-50/50' : 'border-gray-100 hover:bg-gray-50'
      }`}
    >
      <span
        className={`flex h-10 w-12 shrink-0 items-center justify-center rounded-lg text-[11px] font-bold tracking-wide ${providerAccent(method.provider)}`}
      >
        {providerInitials(method.provider)}
      </span>
      <span className="min-w-0 flex-1">
        <span className="block truncate text-sm font-semibold text-ink">{method.provider}</span>
        <span className="block text-xs text-muted">{method.number}</span>
      </span>
      {method.primary ? (
        <Badge tone="green">Primary</Badge>
      ) : (
        <>
          <button
            type="button"
            onClick={() => onSetPrimary(method.id)}
            className="shrink-0 text-xs font-semibold text-brand hover:underline"
          >
            Set primary
          </button>
          <button
            type="button"
            onClick={() => onRemove(method.id)}
            aria-label={`Remove ${method.provider}`}
            className="shrink-0 text-xs font-semibold text-muted hover:text-rose-600"
          >
            ×
          </button>
        </>
      )}
    </div>
  );
}

function PaymentRow({
  payment,
  paying,
  onPay,
  onDownload,
}: {
  payment: Payment;
  paying: boolean;
  onPay: (p: Payment) => void;
  onDownload: (p: Payment) => void;
}) {
  const meta = STATUS[payment.status];
  return (
    <div className="flex items-center gap-4 px-5 py-4 transition-colors hover:bg-gray-50/70">
      <span
        className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-full ${STATUS_CHIP[payment.status]}`}
      >
        {payment.status === 'paid' ? (
          <CheckIcon size={16} />
        ) : payment.status === 'due' ? (
          <ClockIcon size={16} />
        ) : (
          <CalendarIcon size={16} />
        )}
      </span>

      <span className="min-w-0 flex-1">
        <span className="block truncate text-sm font-semibold text-ink">{payment.description}</span>
        <span className="block truncate text-xs text-muted">{payment.property}</span>
      </span>

      <span className="hidden w-32 shrink-0 text-sm text-muted md:block">{payment.date}</span>
      <span className="hidden w-32 shrink-0 truncate text-sm text-muted lg:block">{payment.method}</span>

      <span className="w-24 shrink-0 text-right text-sm font-bold text-ink">
        {formatCedi(payment.amount)}
      </span>

      <span className="hidden w-24 shrink-0 text-right sm:block">
        <Badge tone={meta.tone}>{meta.label}</Badge>
      </span>

      <span className="w-20 shrink-0 text-right">
        {payment.status === 'due' && (
          <Button size="sm" onClick={() => onPay(payment)} disabled={paying}>
            {paying ? '…' : 'Pay'}
          </Button>
        )}
        {payment.status === 'paid' && (
          <button
            type="button"
            onClick={() => onDownload(payment)}
            className="text-xs font-semibold text-brand hover:underline"
          >
            Receipt
          </button>
        )}
      </span>
    </div>
  );
}

function PaymentsView({
  initialPayments, initialMethods,
}: { initialPayments: Payment[]; initialMethods: PaymentMethod[] }) {
  const [payments, setPayments] = useState(initialPayments);
  const [methods, setMethods] = useState(initialMethods);
  const [filter, setFilter] = useState<'all' | PaymentStatus>('all');
  const [payingId, setPayingId] = useState<string | null>(null);
  const [banner, setBanner] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!banner) return;
    const t = setTimeout(() => setBanner(null), 3000);
    return () => clearTimeout(t);
  }, [banner]);

  const primary = methods.find((m) => m.primary) ?? methods[0];
  const nextDue = payments.find((p) => p.status === 'due');

  const totals = useMemo(() => {
    const sum = (status: PaymentStatus) =>
      payments.filter((p) => p.status === status).reduce((acc, p) => acc + p.amount, 0);
    return { paid: sum('paid'), due: sum('due'), upcoming: sum('upcoming') };
  }, [payments]);

  const counts = useMemo(() => {
    const c: Record<'all' | PaymentStatus, number> = { all: payments.length, paid: 0, due: 0, upcoming: 0 };
    payments.forEach((p) => { c[p.status] += 1; });
    return c;
  }, [payments]);

  const visible = filter === 'all' ? payments : payments.filter((p) => p.status === filter);

  const pay = async (payment: Payment) => {
    setError(null);
    setPayingId(payment.id);
    try {
      const intent = await initiatePayment({
        amount: payment.amount,
        channel: channelFor(primary),
        email: getSession()?.email ?? '',
      });
      const txn = await verifyPayment(intent.reference);
      if (txn.status !== 'success') throw new Error('Payment was not completed.');
      setPayments((ps) =>
        ps.map((p) =>
          p.id === payment.id ? { ...p, status: 'paid', method: primary?.provider ?? p.method } : p,
        ),
      );
      setBanner(`${formatCedi(payment.amount)} paid for ${payment.description}.`);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Payment failed. Please try again.');
    } finally {
      setPayingId(null);
    }
  };

  const download = (payment: Payment) => {
    setError(null);
    downloadReceipt(payment.id).catch(() =>
      setError('Could not download that receipt. Please try again.'),
    );
  };

  const addMethod = async (provider: string, digits: string) => {
    const channel = /card|visa|master/i.test(provider) ? 'card' : 'momo';
    try {
      const saved = await addPaymentMethod(provider, `•••• ${digits.slice(-4)}`, channel, methods.length === 0);
      setMethods((ms) => [...ms, saved]);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not save that payment method.');
    }
  };

  const setPrimary = (id: string) => {
    setMethods((ms) => ms.map((m) => ({ ...m, primary: m.id === id })));
    setPrimaryPaymentMethod(id).catch(() => {});
  };

  const removeMethod = (id: string) => {
    setMethods((ms) => ms.filter((m) => m.id !== id));
    deletePaymentMethod(id).catch(() => {});
  };

  return (
    <div className="space-y-6">
      {banner && (
        <div className="flex items-center gap-2 rounded-xl border border-brand-50 bg-brand-50 px-4 py-3 text-sm font-medium text-brand">
          <CheckIcon size={16} /> {banner}
        </div>
      )}
      {error && (
        <div className="rounded-xl border border-rose-100 bg-rose-50 px-4 py-3 text-sm font-medium text-rose-600" role="alert">
          {error}
        </div>
      )}

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <StatCard
          label="Paid to date"
          value={formatCedi(totals.paid)}
          hint={`${counts.paid} payment${counts.paid === 1 ? '' : 's'} settled`}
          icon={<CheckIcon size={18} />}
          chip="bg-brand-50 text-brand"
        />
        <StatCard
          label="Due now"
          value={formatCedi(totals.due)}
          hint={counts.due > 0 ? `${counts.due} payment${counts.due === 1 ? '' : 's'} awaiting you` : 'Nothing due — nice'}
          icon={<ClockIcon size={18} />}
          chip="bg-amber-50 text-amber-600"
        />
        <StatCard
          label="Upcoming"
          value={formatCedi(totals.upcoming)}
          hint={`${counts.upcoming} scheduled payment${counts.upcoming === 1 ? '' : 's'}`}
          icon={<CalendarIcon size={18} />}
          chip="bg-gray-100 text-gray-500"
        />
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-[1fr_320px]">
        <div className="min-w-0 space-y-6">
          {nextDue && (
            <Card className="relative overflow-hidden bg-gradient-to-br from-brand to-emerald-900 p-6 text-white">
              <div className="pointer-events-none absolute -right-10 -top-10 h-40 w-40 rounded-full bg-white/5" />
              <div className="pointer-events-none absolute -bottom-14 right-16 h-32 w-32 rounded-full bg-white/5" />
              <div className="relative flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div>
                  <p className="text-sm font-medium text-white/70">Next payment due</p>
                  <p className="mt-1 text-3xl font-bold">{formatCedi(nextDue.amount)}</p>
                  <p className="mt-1 text-sm text-white/70">
                    {nextDue.description} · due {nextDue.date}
                  </p>
                  {primary && (
                    <p className="mt-3 inline-flex items-center gap-1.5 rounded-full bg-white/10 px-3 py-1 text-xs font-medium text-white/90">
                      <CardIcon size={13} /> Pays with {primary.provider} {primary.number}
                    </p>
                  )}
                </div>
                <Button
                  className="shrink-0 bg-white text-brand hover:bg-white/90"
                  onClick={() => pay(nextDue)}
                  disabled={payingId === nextDue.id}
                >
                  {payingId === nextDue.id ? 'Processing…' : 'Pay now'}
                </Button>
              </div>
            </Card>
          )}

          <Card>
            <div className="flex flex-wrap items-center justify-between gap-3 border-b border-gray-100 px-5 py-4">
              <h2 className="font-bold text-ink">Payment history</h2>
              <div className="flex gap-1.5">
                {FILTERS.map((f) => (
                  <button
                    key={f.id}
                    type="button"
                    onClick={() => setFilter(f.id)}
                    className={`rounded-full px-3 py-1.5 text-xs font-semibold transition-colors ${
                      filter === f.id
                        ? 'bg-ink text-white'
                        : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
                    }`}
                  >
                    {f.label}
                    <span className={`ml-1 ${filter === f.id ? 'text-white/60' : 'text-gray-400'}`}>
                      {counts[f.id]}
                    </span>
                  </button>
                ))}
              </div>
            </div>

            {visible.length === 0 ? (
              <p className="px-5 py-10 text-center text-sm text-muted">
                No {filter === 'all' ? '' : `${STATUS[filter as PaymentStatus].label.toLowerCase()} `}payments to show.
              </p>
            ) : (
              <div className="divide-y divide-gray-100">
                {visible.map((p) => (
                  <PaymentRow
                    key={p.id}
                    payment={p}
                    paying={payingId === p.id}
                    onPay={pay}
                    onDownload={download}
                  />
                ))}
              </div>
            )}
          </Card>
        </div>

        <div className="space-y-6">
          <Card className="h-fit p-5">
            <h2 className="mb-3 font-bold text-ink">Payment methods</h2>
            <div className="space-y-2">
              {methods.map((m) => (
                <MethodRow key={m.id} method={m} onSetPrimary={setPrimary} onRemove={removeMethod} />
              ))}
              <AddMethodForm onAdd={addMethod} />
            </div>
          </Card>

          <Card className="flex items-start gap-3 border-brand-50 bg-brand-50/60 p-4">
            <span className="mt-0.5 shrink-0 text-brand">
              <ShieldIcon size={18} />
            </span>
            <p className="text-xs leading-relaxed text-ink/80">
              Payments are processed securely by Paystack and held in escrow until your stay is
              confirmed. TripNest never stores your card or wallet details.
            </p>
          </Card>
        </div>
      </div>
    </div>
  );
}

export default function PaymentsPage() {
  const paymentsState = useAsync(getPayments, []);
  const methodsState = useAsync(getPaymentMethods, []);

  return (
    <div>
      <h1 className="text-3xl font-bold text-ink">Payments</h1>
      <p className="mb-6 mt-1 text-sm text-muted">
        Track rent and deposits, settle what's due, and manage how you pay.
      </p>
      <AsyncBoundary state={paymentsState} loadingMessage="Loading payments…" errorMessage="Failed to load payments.">
        {(payments) => (
          <AsyncBoundary state={methodsState} loadingMessage="Loading payments…" errorMessage="Failed to load methods.">
            {(methods) => <PaymentsView initialPayments={payments} initialMethods={methods} />}
          </AsyncBoundary>
        )}
      </AsyncBoundary>

      <RentAndSharesSection />
      <TenantClaimsSection />
    </div>
  );
}
