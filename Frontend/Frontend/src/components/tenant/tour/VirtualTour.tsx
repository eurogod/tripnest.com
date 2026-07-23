import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { getPropertyTour, releaseTourMedia } from '../../../api/tours';
import { useAsync } from '../../../hooks/useAsync';
import { useFocusTrap } from '../../../hooks/useFocusTrap';
import RoomsPlayer from './RoomsPlayer';
import FullVideoPlayer from './FullVideoPlayer';

type TourMode = 'rooms' | 'full';

interface VirtualTourProps {
  propertyId: string;
  onClose: () => void;
}

/**
 * Full-screen immersive walkthrough overlay for a property. Renders into a
 * portal on document.body, so callers can mount it from anywhere. Offers two
 * modes when a continuous walkthrough video exists: room-by-room clips
 * (Stories-style) or the full video with chapters.
 */
export default function VirtualTour({ propertyId, onClose }: VirtualTourProps) {
  const state = useAsync(() => getPropertyTour(propertyId), [propertyId]);
  const [mode, setMode] = useState<TourMode>('rooms');
  const [muted, setMuted] = useState(true);
  const containerRef = useRef<HTMLDivElement>(null);
  useFocusTrap(containerRef);

  // Lock body scroll while the overlay is open.
  useEffect(() => {
    const prev = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.body.style.overflow = prev;
    };
  }, []);

  // Release generated-clip object URLs when the tour closes.
  useEffect(() => () => releaseTourMedia(propertyId), [propertyId]);

  const tour = state.data;
  const fullVideoReady =
    tour?.fullVideo?.status === 'ready' &&
    (!!tour.fullVideo.url || (tour.fullVideo.segments?.length ?? 0) > 0);
  const toggleMuted = () => setMuted((m) => !m);
  const modeToggle = fullVideoReady ? (
    <ModeToggle mode={mode} onChange={setMode} />
  ) : undefined;

  return createPortal(
    <div
      ref={containerRef}
      role="dialog"
      aria-modal="true"
      aria-label={tour ? `Virtual tour of ${tour.title}` : 'Virtual tour'}
      className="fixed inset-0 z-[100] flex flex-col bg-black/95 text-white"
    >
      {state.loading && (
        <div className="flex flex-1 items-center justify-center text-sm text-white/70">
          Preparing your walkthrough…
        </div>
      )}
      {state.error && (
        <div className="flex flex-1 flex-col items-center justify-center gap-3">
          <p className="text-white/70">Couldn’t load this tour.</p>
          <button onClick={onClose} className="rounded-lg bg-white/10 px-4 py-2 text-sm">
            Close
          </button>
        </div>
      )}
      {!state.loading && !state.error && tour && (
        mode === 'full' && fullVideoReady ? (
          <FullVideoPlayer
            tour={tour}
            onClose={onClose}
            muted={muted}
            onToggleMuted={toggleMuted}
            modeToggle={modeToggle}
          />
        ) : (
          <RoomsPlayer
            tour={tour}
            onClose={onClose}
            muted={muted}
            onToggleMuted={toggleMuted}
            modeToggle={modeToggle}
          />
        )
      )}
      {!state.loading && !state.error && !tour && (
        <div className="flex flex-1 flex-col items-center justify-center gap-3">
          <p className="text-white/70">No tour available for this listing yet.</p>
          <button onClick={onClose} className="rounded-lg bg-white/10 px-4 py-2 text-sm">
            Close
          </button>
        </div>
      )}
    </div>,
    document.body,
  );
}

function ModeToggle({ mode, onChange }: { mode: TourMode; onChange: (m: TourMode) => void }) {
  const options: { value: TourMode; label: string }[] = [
    { value: 'rooms', label: 'Rooms' },
    { value: 'full', label: 'Full video' },
  ];
  return (
    <div className="flex shrink-0 rounded-full bg-white/15 p-0.5 backdrop-blur" role="group" aria-label="Tour mode">
      {options.map((o) => (
        <button
          key={o.value}
          onClick={() => onChange(o.value)}
          aria-pressed={mode === o.value}
          className={`rounded-full px-2.5 py-1 text-[11px] font-semibold transition-colors ${
            mode === o.value ? 'bg-white text-black' : 'text-white/80 hover:text-white'
          }`}
        >
          {o.label}
        </button>
      ))}
    </div>
  );
}
