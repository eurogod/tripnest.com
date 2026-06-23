import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { propertiesApi } from '@/lib/services';
import { PageHeader, SectionCard } from '@/components/dashboard';
import { Button, Input, Field } from '@/components/ui';
import { Camera, X } from '@/components/icons';
import { TOWNS } from '@/lib/hooks';
import { useToast } from '@/components/Toast';
import { ApiError } from '@/lib/api';
import { CancellationPolicy, StayType, CancellationPolicyLabel, StayTypeLabel } from '@/lib/enums';
import type { CreatePropertyRequest } from '@/types/api';

const PROPERTY_TYPES = ['Apartment', 'House', 'Studio', 'Townhouse', 'Hostel', 'Single room', 'Self-contained'];
const AMENITIES = ['WiFi', 'Air conditioning', 'Parking', 'Water (24/7)', 'Backup power', 'Furnished', 'Kitchen', 'Security', 'Washing machine', 'TV'];

export default function NewListing() {
  const navigate = useNavigate();
  const qc = useQueryClient();
  const toast = useToast();

  const [form, setForm] = useState<CreatePropertyRequest>({
    title: '',
    description: '',
    location: 'Accra',
    latitude: TOWNS.Accra[0],
    longitude: TOWNS.Accra[1],
    bedrooms: 1,
    bathrooms: 1,
    monthlyRent: 0,
    dailyRate: null,
    propertyType: 'Apartment',
    stayType: StayType.LongTerm,
    cancellationPolicy: CancellationPolicy.Moderate,
    amenities: '',
  });
  const [amenities, setAmenities] = useState<string[]>([]);
  const [photos, setPhotos] = useState<{ file: File; url: string }[]>([]);
  const [busy, setBusy] = useState(false);

  const set = <K extends keyof CreatePropertyRequest>(k: K, v: CreatePropertyRequest[K]) => setForm((f) => ({ ...f, [k]: v }));

  function pickTown(town: string) {
    const c = TOWNS[town];
    setForm((f) => ({ ...f, location: town, latitude: c[0], longitude: c[1] }));
  }

  function onFiles(e: React.ChangeEvent<HTMLInputElement>) {
    const files = Array.from(e.target.files ?? []);
    setPhotos((prev) => [...prev, ...files.map((file) => ({ file, url: URL.createObjectURL(file) }))].slice(0, 8));
  }

  function toggleAmenity(a: string) {
    setAmenities((prev) => (prev.includes(a) ? prev.filter((x) => x !== a) : [...prev, a]));
  }

  const isShort = form.stayType === StayType.ShortTerm;

  async function submit() {
    setBusy(true);
    try {
      const created = await propertiesApi.create({ ...form, amenities: amenities.join(', ') });
      if (photos.length) {
        await propertiesApi.uploadPhotos(created.propertyId, photos.map((p) => p.file));
      }
      toast.success('Listing created');
      qc.invalidateQueries({ queryKey: ['my-properties'] });
      navigate('/host/listings');
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Could not create listing');
    } finally {
      setBusy(false);
    }
  }

  const valid = form.title.trim() && form.description.trim() && form.monthlyRent > 0;

  return (
    <div className="max-w-2xl">
      <PageHeader title="Add a property" subtitle="List a verified home. You can edit photos and details later." />

      <div className="space-y-5">
        <SectionCard title="Basics">
          <div className="space-y-4">
            <Input label="Title" placeholder="Cozy 2-bedroom in East Legon" value={form.title} onChange={(e) => set('title', e.target.value)} />
            <Field label="Description">
              <textarea
                className="input min-h-[110px] resize-y"
                placeholder="Describe the space, the neighbourhood, what makes it special…"
                value={form.description}
                onChange={(e) => set('description', e.target.value)}
              />
            </Field>
            <Field label="Property type">
              <select className="input" value={form.propertyType} onChange={(e) => set('propertyType', e.target.value)}>
                {PROPERTY_TYPES.map((t) => (
                  <option key={t} value={t}>
                    {t}
                  </option>
                ))}
              </select>
            </Field>
          </div>
        </SectionCard>

        <SectionCard title="Location">
          <Field label="Town / city" hint="We pin the map to the town centre — you can fine-tune later.">
            <select className="input" value={form.location} onChange={(e) => pickTown(e.target.value)}>
              {Object.keys(TOWNS).map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </select>
          </Field>
        </SectionCard>

        <SectionCard title="Space & pricing">
          <div className="grid grid-cols-2 gap-4">
            <Input label="Bedrooms" type="number" min={0} value={form.bedrooms} onChange={(e) => set('bedrooms', Number(e.target.value))} />
            <Input label="Bathrooms" type="number" min={0} value={form.bathrooms} onChange={(e) => set('bathrooms', Number(e.target.value))} />
          </div>
          <div className="mt-4">
            <Field label="Stay type">
              <select className="input" value={form.stayType} onChange={(e) => set('stayType', Number(e.target.value))}>
                {[StayType.ShortTerm, StayType.LongTerm, StayType.Student].map((v) => (
                  <option key={v} value={v}>
                    {StayTypeLabel[v]}
                  </option>
                ))}
              </select>
            </Field>
          </div>
          <div className="mt-4 grid grid-cols-2 gap-4">
            <Input
              label={isShort ? 'Monthly rent (GHS)' : 'Rent (GHS)'}
              type="number"
              min={0}
              value={form.monthlyRent}
              onChange={(e) => set('monthlyRent', Number(e.target.value))}
            />
            {isShort && (
              <Input
                label="Nightly rate (GHS)"
                type="number"
                min={0}
                value={form.dailyRate ?? 0}
                onChange={(e) => set('dailyRate', Number(e.target.value))}
              />
            )}
          </div>
          <div className="mt-4">
            <Field label="Cancellation policy">
              <select className="input" value={form.cancellationPolicy} onChange={(e) => set('cancellationPolicy', Number(e.target.value))}>
                {[CancellationPolicy.Flexible, CancellationPolicy.Moderate, CancellationPolicy.Strict].map((v) => (
                  <option key={v} value={v}>
                    {CancellationPolicyLabel[v]}
                  </option>
                ))}
              </select>
            </Field>
          </div>
        </SectionCard>

        <SectionCard title="Amenities">
          <div className="flex flex-wrap gap-2">
            {AMENITIES.map((a) => {
              const on = amenities.includes(a);
              return (
                <button
                  key={a}
                  type="button"
                  onClick={() => toggleAmenity(a)}
                  className={`pill border transition ${on ? 'border-brand-600 bg-brand-50 text-brand-700' : 'border-line bg-white text-muted'}`}
                >
                  {a}
                </button>
              );
            })}
          </div>
        </SectionCard>

        <SectionCard title="Photos">
          <div className="grid grid-cols-3 gap-3 sm:grid-cols-4">
            {photos.map((p, i) => (
              <div key={i} className="relative aspect-square overflow-hidden rounded-lg bg-line">
                <img src={p.url} alt="" className="h-full w-full object-cover" />
                <button
                  type="button"
                  onClick={() => setPhotos((prev) => prev.filter((_, idx) => idx !== i))}
                  className="absolute right-1 top-1 grid h-6 w-6 place-items-center rounded-full bg-ink/70 text-white"
                  aria-label="Remove photo"
                >
                  <X className="h-3.5 w-3.5" />
                </button>
              </div>
            ))}
            {photos.length < 8 && (
              <label className="grid aspect-square cursor-pointer place-items-center rounded-lg border-2 border-dashed border-line text-muted hover:border-brand-600 hover:text-brand-600">
                <span className="flex flex-col items-center gap-1 text-xs">
                  <Camera className="h-5 w-5" /> Add
                </span>
                <input type="file" accept="image/*" multiple className="hidden" onChange={onFiles} />
              </label>
            )}
          </div>
          <p className="mt-2 text-xs text-muted">Up to 8 photos. The first becomes your cover image.</p>
        </SectionCard>

        <div className="flex justify-end gap-3 pb-4">
          <Button variant="ghost" onClick={() => navigate('/host/listings')}>
            Cancel
          </Button>
          <Button loading={busy} disabled={!valid} onClick={submit}>
            Publish listing
          </Button>
        </div>
      </div>
    </div>
  );
}
