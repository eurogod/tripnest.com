import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import type { Listing } from '../../types';
import { createListing, uploadListingPhotos, type NewListingInput } from '../../api/listings';
import {
  uploadWalkthroughFile, WALKTHROUGH_VIDEO_EXTENSIONS, WALKTHROUGH_VIDEO_MAX_BYTES,
} from '../../api/walkthroughs';
import { cacheListingPhotos } from '../../lib/listingPhotos';
import { generateWalkthroughClips } from '../../lib/walkthroughGenerator';
import Card from '../ui/Card';
import Button from '../ui/Button';
import { PlusIcon } from '../tenant/icons';

import ListingFields, { FieldLabel } from './ListingFormFields';

/**
 * Full add-property form posting to the real API. Renders as a trigger button
 * that opens a modal; calls onCreated with the saved listing so callers can
 * refresh their lists.
 */
interface PickedPhoto {
  file: File;
  preview: string; // object URL, revoked on removal/unmount
}

export default function AddListingModal({ onCreated, triggerLabel = '+ Add listing' }: {
  onCreated: (listing: Listing) => void;
  triggerLabel?: string;
}) {
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [photos, setPhotos] = useState<PickedPhoto[]>([]);
  const [video, setVideo] = useState<{ file: File; preview: string } | null>(null);
  const [videoError, setVideoError] = useState<string | null>(null);
  // Set when the listing saved but its media didn't — blocks a duplicate resubmit.
  const [uploadsFailed, setUploadsFailed] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const videoInputRef = useRef<HTMLInputElement>(null);

  const [form, setForm] = useState<NewListingInput>({
    title: '',
    description: '',
    location: '',
    bedrooms: 1,
    bathrooms: 1,
    monthlyRent: 0,
    dailyRate: undefined,
    propertyType: 'Apartment',
    stayType: 0,
    cancellationPolicy: 1,
    amenities: '',
  });

  const set = <K extends keyof NewListingInput>(key: K, value: NewListingInput[K]) =>
    setForm((f) => ({ ...f, [key]: value }));

  // Release preview object URLs when the modal unmounts.
  useEffect(() => () => {
    setPhotos((current) => {
      current.forEach((p) => URL.revokeObjectURL(p.preview));
      return current;
    });
    setVideo((current) => {
      if (current) URL.revokeObjectURL(current.preview);
      return current;
    });
  }, []);

  const addPhotos = (files: FileList | null) => {
    if (!files) return;
    const picked = Array.from(files)
      .filter((f) => f.type.startsWith('image/'))
      .map((file) => ({ file, preview: URL.createObjectURL(file) }));
    setPhotos((p) => [...p, ...picked]);
    // Allow re-selecting the same files after a removal.
    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  const pickVideo = (files: FileList | null) => {
    const file = files?.[0];
    if (!file) return;
    setVideoError(null);
    const ext = `.${file.name.split('.').pop()?.toLowerCase() ?? ''}`;
    if (!(WALKTHROUGH_VIDEO_EXTENSIONS as readonly string[]).includes(ext)) {
      setVideoError(`Unsupported video format. Allowed: ${WALKTHROUGH_VIDEO_EXTENSIONS.join(', ')}`);
    } else if (file.size > WALKTHROUGH_VIDEO_MAX_BYTES) {
      setVideoError(`The video exceeds the ${WALKTHROUGH_VIDEO_MAX_BYTES / (1024 * 1024)} MB limit`);
    } else {
      setVideo((current) => {
        if (current) URL.revokeObjectURL(current.preview);
        return { file, preview: URL.createObjectURL(file) };
      });
    }
    // Allow re-selecting the same file after a removal.
    if (videoInputRef.current) videoInputRef.current.value = '';
  };

  const removeVideo = () => {
    setVideo((current) => {
      if (current) URL.revokeObjectURL(current.preview);
      return null;
    });
    setVideoError(null);
  };

  const removePhoto = (preview: string) => {
    setPhotos((p) => {
      const target = p.find((x) => x.preview === preview);
      if (target) URL.revokeObjectURL(target.preview);
      return p.filter((x) => x.preview !== preview);
    });
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
    let listing: Listing;
    try {
      listing = await createListing({
        ...form,
        title: form.title.trim(),
        description: form.description.trim(),
        location: form.location.trim(),
        amenities: form.amenities?.trim(),
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not create the listing. Please try again.');
      setSaving(false);
      return;
    }
    try {
      if (photos.length > 0) {
        const files = photos.map((p) => p.file);
        await uploadListingPhotos(listing.id, files);
        await cacheListingPhotos(listing.id, files);
        // Kick off Veo walkthrough-video generation in the background unless
        // the landlord uploaded their own walkthrough video; the tour shows
        // the photos (marked "generating") until clips land.
        if (!video) void generateWalkthroughClips(listing.id, files);
      }
    } catch (err) {
      // The listing exists; only the photos failed. Surface that without
      // letting a resubmit create a duplicate.
      onCreated(listing);
      setUploadsFailed(true);
      setError(
        err instanceof Error
          ? `Listing created, but the photos could not be uploaded: ${err.message}`
          : 'Listing created, but the photos could not be uploaded. You can add them later.',
      );
      setSaving(false);
      return;
    }
    try {
      if (video) {
        await uploadWalkthroughFile(listing.id, `${form.title.trim()} — walkthrough`, video.file);
      }
      onCreated(listing);
      setOpen(false);
    } catch (err) {
      onCreated(listing);
      setUploadsFailed(true);
      setError(
        err instanceof Error
          ? `Listing created, but the walkthrough video could not be uploaded: ${err.message}`
          : 'Listing created, but the walkthrough video could not be uploaded. You can add it later.',
      );
    } finally {
      setSaving(false);
    }
  };

  if (!open) {
    return (
      <Button
        type="button"
        variant="primary"
        onClick={() => setOpen(true)}
        // Stop a click on the button from starting a text selection on the
        // surrounding heading, and keep the label itself unselectable.
        onMouseDown={(e) => e.preventDefault()}
        className="select-none gap-2 whitespace-nowrap px-6 py-3 text-base shadow-sm"
      >
        <PlusIcon size={18} />
        {triggerLabel.replace(/^\+\s*/, '')}
      </Button>
    );
  }

  // Render through a portal on <body> so no ancestor on the Overview page
  // (flex layout, entrance animation, stacking context) can clip or misplace
  // the fixed overlay — it always covers the whole viewport.
  return createPortal(
    <div
      className="fixed inset-0 z-[100] flex items-center justify-center bg-black/40 p-3 sm:p-4"
      role="dialog"
      aria-modal="true"
      aria-label="Add a new listing"
    >
      {/* Capped to the viewport with an internal scroll: the header stays put and
          the form scrolls, so the top is never pushed off-screen on short windows. */}
      <Card className="flex max-h-[calc(100dvh-1.5rem)] w-full max-w-lg flex-col overflow-hidden p-0">
        <div className="flex shrink-0 items-start justify-between gap-4 border-b border-gray-100 px-5 py-4">
          <div>
            <h2 className="text-lg font-bold text-ink">Add a new listing</h2>
            <p className="mt-0.5 text-sm text-muted">
              It's saved as a draft — publish it from My Listings whenever you're ready.
            </p>
          </div>
          <button
            type="button"
            onClick={() => setOpen(false)}
            className="rounded-lg px-2 py-1 text-xl leading-none text-muted transition-colors hover:bg-gray-100 hover:text-ink"
            aria-label="Close"
          >
            ×
          </button>
        </div>

        <form onSubmit={submit} className="min-h-0 flex-1 space-y-4 overflow-y-auto px-5 py-5">
          <ListingFields form={form} set={set} autoFocusTitle />

          <div>
            <FieldLabel>Photos</FieldLabel>
            <p className="mb-2 text-xs text-muted">
              Add photos of every room — they power the listing gallery and the AI video
              walkthrough guests use to tour the property.
            </p>
            <input
              ref={fileInputRef}
              type="file"
              accept="image/*"
              multiple
              onChange={(e) => addPhotos(e.target.files)}
              className="hidden"
              aria-label="Add listing photos"
            />
            <div className="flex flex-wrap gap-2">
              {photos.map((p) => (
                <div key={p.preview} className="relative h-20 w-20 overflow-hidden rounded-lg border border-gray-200">
                  <img src={p.preview} alt="" className="h-full w-full object-cover" />
                  <button
                    type="button"
                    onClick={() => removePhoto(p.preview)}
                    aria-label="Remove photo"
                    className="absolute right-1 top-1 flex h-5 w-5 items-center justify-center rounded-full bg-black/60 text-xs leading-none text-white hover:bg-black/80"
                  >
                    ×
                  </button>
                </div>
              ))}
              <button
                type="button"
                onClick={() => fileInputRef.current?.click()}
                className="flex h-20 w-20 flex-col items-center justify-center gap-1 rounded-lg border border-dashed border-gray-300 text-muted transition-colors hover:border-brand hover:text-brand"
              >
                <span className="text-xl leading-none">+</span>
                <span className="text-[11px]">Add photos</span>
              </button>
            </div>
            {photos.length > 0 && (
              <p className="mt-1.5 text-xs text-muted">
                {photos.length} photo{photos.length === 1 ? '' : 's'} selected
              </p>
            )}
          </div>

          <div>
            <FieldLabel>Video walkthrough (optional)</FieldLabel>
            <p className="mb-2 text-xs text-muted">
              Upload a video tour of the rooms in your photos. It goes to an admin for
              approval before it appears on Explore.
            </p>
            <input
              ref={videoInputRef}
              type="file"
              accept={WALKTHROUGH_VIDEO_EXTENSIONS.join(',')}
              onChange={(e) => pickVideo(e.target.files)}
              className="hidden"
              aria-label="Add walkthrough video"
            />
            {video ? (
              <div className="overflow-hidden rounded-lg border border-gray-200">
                <video src={video.preview} controls playsInline className="max-h-56 w-full bg-black" />
                <div className="flex items-center justify-between gap-2 px-3 py-2">
                  <p className="min-w-0 truncate text-xs text-muted">
                    {video.file.name} · {(video.file.size / (1024 * 1024)).toFixed(1)} MB
                  </p>
                  <button
                    type="button"
                    onClick={removeVideo}
                    className="shrink-0 text-xs font-medium text-rose-600 hover:text-rose-700"
                  >
                    Remove
                  </button>
                </div>
              </div>
            ) : (
              <button
                type="button"
                onClick={() => videoInputRef.current?.click()}
                className="flex h-20 w-full flex-col items-center justify-center gap-1 rounded-lg border border-dashed border-gray-300 text-muted transition-colors hover:border-brand hover:text-brand"
              >
                <span className="text-xl leading-none">+</span>
                <span className="text-[11px]">Add video walkthrough</span>
              </button>
            )}
            {videoError && (
              <p className="mt-1.5 text-xs text-rose-600" role="alert">
                {videoError}
              </p>
            )}
          </div>

          {error && (
            <p className="rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-600" role="alert">
              {error}
            </p>
          )}

          <div className="sticky bottom-0 -mx-5 -mb-5 flex justify-end gap-2 border-t border-gray-100 bg-white px-5 py-3">
            {uploadsFailed ? (
              <Button type="button" onClick={() => setOpen(false)}>
                Close
              </Button>
            ) : (
              <>
                <Button type="button" variant="ghost" onClick={() => setOpen(false)}>
                  Cancel
                </Button>
                <Button type="submit" disabled={!canSubmit}>
                  {saving ? 'Creating…' : 'Create listing'}
                </Button>
              </>
            )}
          </div>
        </form>
      </Card>
    </div>,
    document.body,
  );
}
