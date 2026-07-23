import { useState } from 'react';
import type { NewListingInput } from '../../api/listings';

// Field set shared by the add- and edit-listing modals.

const INPUT =
  'w-full rounded-lg border border-gray-200 bg-white px-3 py-2.5 text-sm text-ink outline-none focus:border-brand';

// The tick-box amenities every listing chooses from; anything else the host
// types goes under "Other". Stored (and searched) as a comma-separated string.
const CANONICAL_AMENITIES = [
  'WiFi', 'TV', 'Air Conditioning', 'Water Tank',
  'Parking', 'Water', 'Surveillance Security', 'Generator',
] as const;

const splitCsv = (csv: string) =>
  csv.split(',').map((t) => t.trim()).filter(Boolean);

/**
 * Amenities as checkboxes for the canonical set plus an "Other" free-text box
 * for extras. Value stays a CSV string so the API and search filter are
 * unchanged; canonical matching is case-insensitive.
 */
function AmenitiesField({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  const tokens = splitCsv(value);
  const canonicalLower = new Set(CANONICAL_AMENITIES.map((a) => a.toLowerCase()));
  const isChecked = (a: string) => tokens.some((t) => t.toLowerCase() === a.toLowerCase());
  const initialCustom = tokens.filter((t) => !canonicalLower.has(t.toLowerCase()));

  const [showOther, setShowOther] = useState(initialCustom.length > 0);
  const [otherText, setOtherText] = useState(initialCustom.join(', '));

  const rebuild = (checkedCanonical: string[], other: string) => {
    onChange([...checkedCanonical, ...splitCsv(other)].join(', '));
  };

  const currentCanonical = () => CANONICAL_AMENITIES.filter(isChecked);

  const toggle = (a: string) => {
    const next = isChecked(a)
      ? currentCanonical().filter((x) => x !== a)
      : [...currentCanonical(), a];
    rebuild(next, otherText);
  };

  const updateOther = (text: string) => {
    setOtherText(text);
    rebuild(currentCanonical(), text);
  };

  return (
    <div>
      <FieldLabel>Amenities</FieldLabel>
      <p className="mb-2 text-xs text-muted">Tick everything currently available at this property.</p>
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-2">
        {CANONICAL_AMENITIES.map((a) => (
          <label
            key={a}
            className={`flex cursor-pointer items-center gap-2.5 rounded-lg border px-3 py-2.5 text-sm transition-colors ${
              isChecked(a)
                ? 'border-brand bg-brand-50 text-ink'
                : 'border-gray-200 bg-white text-gray-600 hover:border-gray-300'
            }`}
          >
            <input
              type="checkbox"
              checked={isChecked(a)}
              onChange={() => toggle(a)}
              className="h-4 w-4 shrink-0 accent-brand"
            />
            <span className="font-medium">{a}</span>
          </label>
        ))}
      </div>

      <div className="mt-2">
        {showOther ? (
          <label className="block">
            <span className="mb-1.5 block text-xs font-medium text-muted">
              Other amenities (comma-separated)
            </span>
            <input
              value={otherText}
              onChange={(e) => updateOther(e.target.value)}
              placeholder="Kitchen, Balcony, Study desk"
              className={INPUT}
              autoFocus
            />
          </label>
        ) : (
          <button
            type="button"
            onClick={() => setShowOther(true)}
            className="rounded-lg border border-dashed border-gray-300 px-3 py-2 text-sm font-medium text-brand transition-colors hover:border-brand hover:bg-brand-50/50"
          >
            + Other
          </button>
        )}
      </div>
    </div>
  );
}

const PROPERTY_TYPES = ['Apartment', 'House', 'Room', 'Studio', 'Hostel', 'Villa'];

const STAY_TYPES = [
  { value: 0, label: 'Short term (nights)' },
  { value: 1, label: 'Long term (months)' },
  { value: 2, label: 'Student housing' },
];

const CANCELLATION_POLICIES = [
  { value: 0, label: 'Flexible — full refund up to 24h before' },
  { value: 1, label: 'Moderate — full refund 5+ days before' },
  { value: 2, label: 'Strict — 50% refund 7+ days before' },
];

export function FieldLabel({ children }: { children: React.ReactNode }) {
  return <span className="mb-1.5 block text-sm font-medium text-ink">{children}</span>;
}

interface ListingFieldsProps {
  form: NewListingInput;
  set: <K extends keyof NewListingInput>(key: K, value: NewListingInput[K]) => void;
  autoFocusTitle?: boolean;
}

export default function ListingFields({ form, set, autoFocusTitle = false }: ListingFieldsProps) {
  return (
    <>
      <label className="block">
        <FieldLabel>Title</FieldLabel>
        <input
          autoFocus={autoFocusTitle}
          value={form.title}
          onChange={(e) => set('title', e.target.value)}
          placeholder="2-Bedroom Apartment near KNUST"
          className={INPUT}
        />
      </label>

      <label className="block">
        <FieldLabel>Description</FieldLabel>
        <textarea
          value={form.description}
          onChange={(e) => set('description', e.target.value)}
          rows={3}
          placeholder="What makes this place great to stay in?"
          className={`${INPUT} resize-y`}
        />
      </label>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <label className="block">
          <FieldLabel>Location</FieldLabel>
          <input
            value={form.location}
            onChange={(e) => set('location', e.target.value)}
            placeholder="Tarkwa, Western Region"
            className={INPUT}
          />
        </label>
        <label className="block">
          <FieldLabel>Property type</FieldLabel>
          <select
            value={form.propertyType}
            onChange={(e) => set('propertyType', e.target.value)}
            className={INPUT}
          >
            {PROPERTY_TYPES.map((t) => (
              <option key={t}>{t}</option>
            ))}
          </select>
        </label>
      </div>

      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <label className="block">
          <FieldLabel>Bedrooms</FieldLabel>
          <input
            type="number"
            min={0}
            value={form.bedrooms}
            onChange={(e) => set('bedrooms', Math.max(0, Number(e.target.value)))}
            className={INPUT}
          />
        </label>
        <label className="block">
          <FieldLabel>Bathrooms</FieldLabel>
          <input
            type="number"
            min={0}
            value={form.bathrooms}
            onChange={(e) => set('bathrooms', Math.max(0, Number(e.target.value)))}
            className={INPUT}
          />
        </label>
        <label className="block col-span-2 sm:col-span-1">
          <FieldLabel>Rent / month</FieldLabel>
          <div className="flex items-center rounded-lg border border-gray-200 bg-white px-3 focus-within:border-brand">
            <span className="text-xs text-muted">GH₵</span>
            <input
              type="number"
              min={0}
              value={form.monthlyRent || ''}
              onChange={(e) => set('monthlyRent', Math.max(0, Number(e.target.value)))}
              className="w-full bg-transparent px-2 py-2.5 text-sm text-ink outline-none"
            />
          </div>
        </label>
        <label className="block col-span-2 sm:col-span-1">
          <FieldLabel>Rate / night</FieldLabel>
          <div className="flex items-center rounded-lg border border-gray-200 bg-white px-3 focus-within:border-brand">
            <span className="text-xs text-muted">GH₵</span>
            <input
              type="number"
              min={0}
              value={form.dailyRate ?? ''}
              onChange={(e) => set('dailyRate', e.target.value === '' ? undefined : Math.max(0, Number(e.target.value)))}
              placeholder="auto"
              className="w-full bg-transparent px-2 py-2.5 text-sm text-ink outline-none"
            />
          </div>
        </label>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <label className="block">
          <FieldLabel>Stay type</FieldLabel>
          <select
            value={form.stayType}
            onChange={(e) => set('stayType', Number(e.target.value))}
            className={INPUT}
          >
            {STAY_TYPES.map((t) => (
              <option key={t.value} value={t.value}>{t.label}</option>
            ))}
          </select>
        </label>
        <label className="block">
          <FieldLabel>Cancellation policy</FieldLabel>
          <select
            value={form.cancellationPolicy}
            onChange={(e) => set('cancellationPolicy', Number(e.target.value))}
            className={INPUT}
          >
            {CANCELLATION_POLICIES.map((p) => (
              <option key={p.value} value={p.value}>{p.label}</option>
            ))}
          </select>
        </label>
      </div>

      <AmenitiesField
        value={form.amenities ?? ''}
        onChange={(v) => set('amenities', v)}
      />
    </>
  );
}
