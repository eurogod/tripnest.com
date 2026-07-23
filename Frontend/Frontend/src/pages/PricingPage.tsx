import { useMemo, useState } from 'react';
import type { Listing, PricingSettings } from '../types';
import { getPricingSettings, savePricingSettings } from '../api/pricing';
import { getListings } from '../api/listings';
import { useAsync } from '../hooks/useAsync';
import AsyncBoundary from '../components/AsyncBoundary';
import Card from '../components/ui/Card';
import Button from '../components/ui/Button';
import { formatCedi } from '../lib/format';
import {
  CalendarIcon,
  CheckIcon,
  ClockIcon,
  HomeIcon,
  SparkleIcon,
} from '../components/tenant/icons';

function Field({
  label,
  value,
  onChange,
  prefix,
  suffix,
  hint,
}: {
  label: string;
  value: number;
  onChange: (value: number) => void;
  prefix?: string;
  suffix?: string;
  hint?: string;
}) {
  return (
    <label className="block">
      <span className="mb-1.5 block text-sm font-medium text-ink">{label}</span>
      <div className="flex items-center rounded-lg border border-gray-200 bg-white px-3 focus-within:border-brand">
        {prefix && <span className="text-sm text-muted">{prefix}</span>}
        <input
          type="number"
          value={value}
          onChange={(e) => onChange(Number(e.target.value))}
          className="w-full bg-transparent px-2 py-2.5 text-ink outline-none"
        />
        {suffix && <span className="text-sm text-muted">{suffix}</span>}
      </div>
      {hint && <span className="mt-1 block text-xs text-muted">{hint}</span>}
    </label>
  );
}

function Stepper({
  label,
  value,
  onChange,
  min = 1,
  max = 30,
  hint,
}: {
  label: string;
  value: number;
  onChange: (value: number) => void;
  min?: number;
  max?: number;
  hint?: string;
}) {
  return (
    <div>
      <span className="mb-1.5 block text-sm font-medium text-ink">{label}</span>
      <div className="flex w-fit items-center rounded-lg border border-gray-200 bg-white">
        <button
          type="button"
          onClick={() => onChange(Math.max(min, value - 1))}
          className="px-4 py-2.5 text-lg font-semibold text-muted transition-colors hover:text-brand disabled:opacity-40"
          disabled={value <= min}
          aria-label={`Decrease ${label.toLowerCase()}`}
        >
          −
        </button>
        <span className="w-12 text-center font-bold text-ink">{value}</span>
        <button
          type="button"
          onClick={() => onChange(Math.min(max, value + 1))}
          className="px-4 py-2.5 text-lg font-semibold text-muted transition-colors hover:text-brand disabled:opacity-40"
          disabled={value >= max}
          aria-label={`Increase ${label.toLowerCase()}`}
        >
          +
        </button>
      </div>
      {hint && <span className="mt-1 block text-xs text-muted">{hint}</span>}
    </div>
  );
}

function Section({
  title,
  desc,
  icon,
  chip,
  children,
}: {
  title: string;
  desc: string;
  icon: React.ReactNode;
  chip: string;
  children: React.ReactNode;
}) {
  return (
    <Card className="p-6">
      <div className="mb-5 flex items-start gap-3">
        <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${chip}`}>
          {icon}
        </span>
        <div>
          <h2 className="text-lg font-bold text-ink">{title}</h2>
          <p className="text-sm text-muted">{desc}</p>
        </div>
      </div>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">{children}</div>
    </Card>
  );
}

/** Example 7-night stay (5 weekday + 2 weekend nights) priced with the current settings. */
function weekPreview(form: PricingSettings) {
  const nightsSubtotal = form.baseRate * 5 + form.weekendRate * 2;
  const discount = Math.round((nightsSubtotal * form.weeklyDiscountPercent) / 100);
  return {
    nightsSubtotal,
    discount,
    total: nightsSubtotal - discount + form.cleaningFee,
  };
}

function PricingForm({ initial, propertyId }: { initial: PricingSettings; propertyId: string }) {
  const [form, setForm] = useState(initial);
  // Compare against the last persisted values, not the first fetch, so the
  // "Saved" state actually appears after a successful PUT.
  const [persisted, setPersisted] = useState(initial);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState('');

  const dirty = useMemo(
    () => (Object.keys(form) as (keyof PricingSettings)[]).some((k) => form[k] !== persisted[k]),
    [form, persisted],
  );
  const weekendLift =
    form.baseRate > 0 ? Math.round(((form.weekendRate - form.baseRate) / form.baseRate) * 100) : 0;
  const week = weekPreview(form);

  const set = (key: keyof PricingSettings) => (value: number) => {
    setForm((f) => ({ ...f, [key]: value }));
    setSaved(false);
  };

  const onSave = async () => {
    setSaving(true);
    setError('');
    try {
      const savedSettings = await savePricingSettings(propertyId, form);
      setPersisted(savedSettings);
      setForm(savedSettings);
      setSaved(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not save pricing.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="grid grid-cols-1 gap-6 lg:grid-cols-[1fr_340px]">
      <div className="min-w-0 space-y-6">
        <Section
          title="Nightly rates"
          desc="What guests pay per night before discounts"
          icon={<HomeIcon size={18} />}
          chip="bg-brand-50 text-brand"
        >
          <Field label="Base rate" prefix="GH₵" value={form.baseRate} onChange={set('baseRate')} />
          <Field
            label="Weekend rate"
            prefix="GH₵"
            value={form.weekendRate}
            onChange={set('weekendRate')}
            hint={
              weekendLift !== 0
                ? `${weekendLift > 0 ? '+' : ''}${weekendLift}% vs weekdays`
                : 'Same as weekdays'
            }
          />
        </Section>

        <Section
          title="Length-of-stay discounts"
          desc="Reward longer stays to lift occupancy"
          icon={<SparkleIcon size={18} />}
          chip="bg-amber-50 text-amber-600"
        >
          <Field
            label="Weekly discount"
            suffix="%"
            value={form.weeklyDiscountPercent}
            onChange={set('weeklyDiscountPercent')}
            hint="Stays of 7+ nights"
          />
          <Field
            label="Monthly discount"
            suffix="%"
            value={form.monthlyDiscountPercent}
            onChange={set('monthlyDiscountPercent')}
            hint="Stays of 28+ nights"
          />
        </Section>

        <Section
          title="Stay rules"
          desc="Booking limits and one-off fees"
          icon={<ClockIcon size={18} />}
          chip="bg-blue-50 text-blue-600"
        >
          <Stepper
            label="Minimum nights"
            value={form.minNights}
            onChange={set('minNights')}
            hint="Shortest stay guests can book"
          />
          <Field
            label="Cleaning fee"
            prefix="GH₵"
            value={form.cleaningFee}
            onChange={set('cleaningFee')}
            hint="Charged once per stay"
          />
        </Section>

        <div className="flex items-center gap-3">
          <Button onClick={onSave} disabled={saving || !dirty}>
            {saving ? 'Saving…' : 'Save changes'}
          </Button>
          {saved && !dirty && (
            <span className="flex items-center gap-1.5 text-sm font-medium text-brand">
              <CheckIcon size={15} /> Saved
            </span>
          )}
          {dirty && !saving && (
            <span className="rounded-full bg-amber-50 px-3 py-1 text-xs font-semibold text-amber-600">
              Unsaved changes
            </span>
          )}
          {error && <span className="text-sm text-rose-600" role="alert">{error}</span>}
        </div>
      </div>

      <aside>
        <Card className="sticky top-6 overflow-hidden">
          <div className="bg-gradient-to-br from-brand to-emerald-900 p-5 text-white">
            <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-white/70">
              <CalendarIcon size={13} /> Guest price preview
            </p>
            <p className="mt-2 text-3xl font-bold">{formatCedi(form.baseRate)}</p>
            <p className="text-sm text-white/70">
              per weekday night · {formatCedi(form.weekendRate)} weekends
            </p>
          </div>

          <div className="p-5">
            <p className="mb-3 text-sm font-semibold text-ink">A typical week (7 nights)</p>
            <div className="space-y-1.5 text-sm">
              <div className="flex justify-between text-muted">
                <span>5 weekday nights</span>
                <span className="text-ink">{formatCedi(form.baseRate * 5)}</span>
              </div>
              <div className="flex justify-between text-muted">
                <span>2 weekend nights</span>
                <span className="text-ink">{formatCedi(form.weekendRate * 2)}</span>
              </div>
              {week.discount > 0 && (
                <div className="flex justify-between text-muted">
                  <span>Weekly discount ({form.weeklyDiscountPercent}%)</span>
                  <span className="font-medium text-brand">−{formatCedi(week.discount)}</span>
                </div>
              )}
              <div className="flex justify-between text-muted">
                <span>Cleaning fee</span>
                <span className="text-ink">{formatCedi(form.cleaningFee)}</span>
              </div>
              <div className="flex justify-between border-t border-gray-100 pt-2 text-base font-bold text-ink">
                <span>Guest pays</span>
                <span>{formatCedi(week.total)}</span>
              </div>
            </div>

            <p className="mt-4 rounded-lg bg-gray-50 p-3 text-xs leading-relaxed text-muted">
              Guests can book {form.minNights}+ night{form.minNights > 1 ? 's' : ''}. Monthly stays
              get {form.monthlyDiscountPercent}% off nightly rates.
            </p>
          </div>
        </Card>
      </aside>
    </div>
  );
}

function ListingPricing({ listings }: { listings: Listing[] }) {
  const [propertyId, setPropertyId] = useState(listings[0].id);
  const state = useAsync(() => getPricingSettings(propertyId), [propertyId]);

  return (
    <>
      <label className="mb-6 block w-full max-w-sm">
        <span className="mb-1.5 block text-sm font-medium text-ink">Listing</span>
        <select
          value={propertyId}
          onChange={(e) => setPropertyId(e.target.value)}
          className="w-full rounded-lg border border-gray-200 bg-white px-3 py-2.5 text-sm text-ink outline-none focus:border-brand"
        >
          {listings.map((l) => (
            <option key={l.id} value={l.id}>{l.title}</option>
          ))}
        </select>
      </label>
      <AsyncBoundary state={state} loadingMessage="Loading pricing…" errorMessage="Failed to load pricing.">
        {(data) => <PricingForm key={propertyId} initial={data} propertyId={propertyId} />}
      </AsyncBoundary>
    </>
  );
}

export default function PricingPage() {
  const listings = useAsync(getListings, []);

  return (
    <div>
      <h1 className="text-4xl font-bold text-ink">Pricing</h1>
      <p className="mb-8 mt-1 text-sm text-muted">
        Set your rates and stay rules per listing — the preview shows exactly what guests will pay.
      </p>
      <AsyncBoundary
        state={listings}
        loadingMessage="Loading listings…"
        errorMessage="Failed to load listings."
        emptyMessage="Add a listing first — pricing is set per listing."
        isEmpty={(rows) => rows.length === 0}
      >
        {(rows) => <ListingPricing listings={rows} />}
      </AsyncBoundary>
    </div>
  );
}
