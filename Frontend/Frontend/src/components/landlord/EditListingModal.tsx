import { useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import type { Listing, ListingPhoto } from '../../types';
import {
  generateListingCopy, getListingById, getListingProperty, removeListingPhoto,
  setListingCover, updateListing, uploadListingPhotos,
  type ListingCopySuggestion, type NewListingInput,
} from '../../api/listings';
import { cacheListingPhotos } from '../../lib/listingPhotos';
import { assetUrl } from '../../api/client';
import { aiErrorMessage } from '../../api/assistant';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../AsyncBoundary';
import Card from '../ui/Card';
import Button from '../ui/Button';
import ListingFields, { FieldLabel } from './ListingFormFields';
import ListingCopySuggestionCard from './ListingCopySuggestionCard';
import { SparkleIcon } from '../tenant/icons';
import type { PropertyResponseDto } from '../../api/backend';

interface EditListingModalProps {
  listingId: string;
  onClose: () => void;
  onUpdated: (listing: Listing) => void;
}

function EditForm({ dto, onClose, onUpdated }: {
  dto: PropertyResponseDto;
  onClose: () => void;
  onUpdated: (listing: Listing) => void;
}) {
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [suggestion, setSuggestion] = useState<ListingCopySuggestion | null>(null);
  const [generating, setGenerating] = useState(false);
  const [aiError, setAiError] = useState<string | null>(null);
  const [photos, setPhotos] = useState<ListingPhoto[]>(
    (dto.photos ?? []).map((p) => ({
      id: p.id, url: assetUrl(p.url), isCover: p.isCover, sortOrder: p.sortOrder,
    })),
  );
  const [photoBusy, setPhotoBusy] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const [photoError, setPhotoError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const chooseCover = async (photoId: string) => {
    setPhotoBusy(photoId);
    setPhotoError(null);
    try {
      const updated = await setListingCover(dto.propertyId, photoId);
      setPhotos(updated.photos);
      onUpdated(updated); // refresh the listing card's cover
    } catch {
      setPhotoError('Could not set the cover. Try again.');
    } finally {
      setPhotoBusy(null);
    }
  };

  const removePhoto = async (photoId: string) => {
    setPhotoBusy(photoId);
    setPhotoError(null);
    try {
      const updated = await removeListingPhoto(dto.propertyId, photoId);
      setPhotos(updated.photos);
      onUpdated(updated);
    } catch {
      setPhotoError('Could not remove the photo. Try again.');
    } finally {
      setPhotoBusy(null);
    }
  };

  const addPhotos = async (files: FileList | null) => {
    const picked = files ? Array.from(files).filter((f) => f.type.startsWith('image/')) : [];
    if (fileInputRef.current) fileInputRef.current.value = ''; // allow re-picking same files
    if (picked.length === 0) return;
    setUploading(true);
    setPhotoError(null);
    try {
      await uploadListingPhotos(dto.propertyId, picked);
      // Keep a local cache too, so galleries work even before a full refetch.
      void cacheListingPhotos(dto.propertyId, picked);
      const updated = await getListingById(dto.propertyId);
      setPhotos(updated.photos);
      onUpdated(updated);
    } catch (e) {
      setPhotoError(e instanceof Error ? e.message : 'Could not upload the photos. Try again.');
    } finally {
      setUploading(false);
    }
  };
  const [form, setForm] = useState<NewListingInput>({
    title: dto.title,
    description: dto.description,
    location: dto.location,
    bedrooms: dto.bedrooms,
    bathrooms: dto.bathrooms,
    monthlyRent: dto.monthlyRent,
    dailyRate: dto.dailyRate ?? undefined,
    propertyType: dto.propertyType,
    stayType: dto.stayType,
    cancellationPolicy: dto.cancellationPolicy,
    amenities: dto.amenities ?? '',
  });

  const set = <K extends keyof NewListingInput>(key: K, value: NewListingInput[K]) =>
    setForm((f) => ({ ...f, [key]: value }));

  const hasPhotos = photos.length > 0 || Boolean(dto.photoPaths?.trim());

  const generate = async () => {
    setAiError(null);
    setGenerating(true);
    try {
      setSuggestion(await generateListingCopy(dto.propertyId));
    } catch (err) {
      setAiError(aiErrorMessage(err));
    } finally {
      setGenerating(false);
    }
  };

  // Highlights are marketing bullets with no field of their own — fold them
  // into the description; nothing persists until the host saves the form.
  const suggestedDescription = (s: ListingCopySuggestion) =>
    s.highlights.length > 0
      ? `${s.description}\n\n${s.highlights.map((h) => `• ${h}`).join('\n')}`
      : s.description;

  const applyTitle = () => { if (suggestion) set('title', suggestion.title); };
  const applyDescription = () => { if (suggestion) set('description', suggestedDescription(suggestion)); };
  const applyAll = () => {
    if (!suggestion) return;
    setForm((f) => ({ ...f, title: suggestion.title, description: suggestedDescription(suggestion) }));
    setSuggestion(null);
  };

  const canSubmit =
    form.title.trim().length > 0 &&
    form.description.trim().length > 0 &&
    form.location.trim().length > 0 &&
    form.monthlyRent > 0 &&
    !saving;

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;
    setError(null);
    setSaving(true);
    try {
      const listing = await updateListing(dto.propertyId, {
        ...form,
        title: form.title.trim(),
        description: form.description.trim(),
        location: form.location.trim(),
        amenities: form.amenities?.trim(),
        latitude: dto.latitude,
        longitude: dto.longitude,
      });
      onUpdated(listing);
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save the changes. Please try again.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <form onSubmit={submit} className="space-y-4">
      <ListingFields form={form} set={set} autoFocusTitle />

      <div>
        <FieldLabel>Photos</FieldLabel>
        <p className="mb-2 text-xs text-muted">
          Tap a photo to make it the cover; use × to remove one, or add more below.
        </p>
        <input
          ref={fileInputRef}
          type="file"
          accept="image/*"
          multiple
          onChange={(e) => void addPhotos(e.target.files)}
          className="hidden"
          aria-label="Add listing photos"
        />
        <div className="grid grid-cols-3 gap-2 sm:grid-cols-4">
          {photos.map((p) => (
            <div
              key={p.id}
              className={`relative aspect-square overflow-hidden rounded-lg border-2 transition-colors ${
                p.isCover ? 'border-brand' : 'border-transparent'
              }`}
            >
              <button
                type="button"
                onClick={() => void chooseCover(p.id)}
                disabled={photoBusy !== null}
                aria-label={p.isCover ? 'Current cover photo' : 'Set as cover photo'}
                aria-pressed={p.isCover}
                className="block h-full w-full disabled:opacity-70"
              >
                <img src={p.url} alt="" className="h-full w-full object-cover" />
              </button>
              {p.isCover && (
                <span className="pointer-events-none absolute inset-x-0 bottom-0 bg-brand/90 py-0.5 text-center text-[10px] font-semibold text-white">
                  Cover
                </span>
              )}
              <button
                type="button"
                onClick={() => void removePhoto(p.id)}
                disabled={photoBusy !== null}
                aria-label="Remove photo"
                className="absolute right-1 top-1 flex h-6 w-6 items-center justify-center rounded-full bg-black/60 text-sm leading-none text-white transition-colors hover:bg-black/80 disabled:opacity-50"
              >
                ×
              </button>
              {photoBusy === p.id && (
                <span className="pointer-events-none absolute inset-0 grid place-items-center bg-black/40 text-[10px] font-medium text-white">
                  Working…
                </span>
              )}
            </div>
          ))}
          <button
            type="button"
            onClick={() => fileInputRef.current?.click()}
            disabled={uploading}
            className="flex aspect-square flex-col items-center justify-center gap-1 rounded-lg border border-dashed border-gray-300 text-muted transition-colors hover:border-brand hover:text-brand disabled:opacity-60"
          >
            <span className="text-xl leading-none">+</span>
            <span className="text-[10px]">{uploading ? 'Uploading…' : 'Add photos'}</span>
          </button>
        </div>
        {photoError && <p className="mt-1.5 text-xs text-rose-600" role="alert">{photoError}</p>}
      </div>

      <div>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          onClick={() => void generate()}
          disabled={generating || saving || !hasPhotos}
        >
          <span className="flex items-center gap-1.5">
            <SparkleIcon size={15} />
            {generating ? 'Writing your listing…' : 'Write it for me'}
          </span>
        </Button>
        {!hasPhotos && (
          <p className="mt-1 text-xs text-muted">
            Upload photos first — the AI writes from your pictures.
          </p>
        )}
        {aiError && (
          <p className="mt-2 rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-600" role="alert">
            {aiError}
          </p>
        )}
      </div>

      {suggestion && (
        <ListingCopySuggestionCard
          suggestion={suggestion}
          onApplyTitle={applyTitle}
          onApplyDescription={applyDescription}
          onApplyAll={applyAll}
          onDismiss={() => setSuggestion(null)}
        />
      )}

      {error && (
        <p className="rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-600" role="alert">
          {error}
        </p>
      )}

      <div className="sticky bottom-0 -mx-5 -mb-5 flex justify-end gap-2 border-t border-gray-100 bg-white px-5 py-3">
        <Button type="button" variant="ghost" onClick={onClose}>
          Cancel
        </Button>
        <Button type="submit" disabled={!canSubmit}>
          {saving ? 'Saving…' : 'Save changes'}
        </Button>
      </div>
    </form>
  );
}

/** Edit form for an existing listing, pre-filled from the backend record. */
export default function EditListingModal({ listingId, onClose, onUpdated }: EditListingModalProps) {
  const state = useAsync(() => getListingProperty(listingId), [listingId]);

  return createPortal(
    <div
      className="fixed inset-0 z-[100] flex items-center justify-center bg-black/40 p-3 sm:p-4"
      role="dialog"
      aria-modal="true"
      aria-label="Edit listing"
    >
      <Card className="flex max-h-[calc(100dvh-1.5rem)] w-full max-w-lg flex-col overflow-hidden p-0">
        <div className="flex shrink-0 items-start justify-between gap-4 border-b border-gray-100 px-5 py-4">
          <div>
            <h2 className="text-lg font-bold text-ink">Edit listing</h2>
            <p className="mt-0.5 text-sm text-muted">
              Changes are saved to your live listing right away.
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg px-2 py-1 text-xl leading-none text-muted transition-colors hover:bg-gray-100 hover:text-ink"
            aria-label="Close"
          >
            ×
          </button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-5">
          <AsyncBoundary
            state={state}
            loadingMessage="Loading listing…"
            errorMessage="Couldn't load this listing."
          >
            {(dto) => <EditForm dto={dto} onClose={onClose} onUpdated={onUpdated} />}
          </AsyncBoundary>
        </div>
      </Card>
    </div>,
    document.body,
  );
}
