import { useCallback, useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import type { PropertyTour, TourHotspot } from '../../../types';
import { PlayIcon, PauseIcon, VolumeIcon, VolumeMuteIcon } from '../icons';
import { useTourPlayback } from './useTourPlayback';
import RoomScene from './RoomScene';
import HotspotDetail from './HotspotDetail';

const SWIPE_MIN_PX = 48;

interface RoomsPlayerProps {
  tour: PropertyTour;
  onClose: () => void;
  muted: boolean;
  onToggleMuted: () => void;
  modeToggle?: ReactNode;
}

/** Stories-style per-room walkthrough: clips/images/gradients with hotspots. */
export default function RoomsPlayer({
  tour, onClose, muted, onToggleMuted, modeToggle,
}: RoomsPlayerProps) {
  const rooms = tour.rooms;
  const [active, setActive] = useState<TourHotspot | null>(null);
  const { index, playing, progress, go, next, prev, togglePlaying, setVideoEl } =
    useTourPlayback(rooms.length, active !== null);
  const room = rooms[index];

  // First upcoming room with a ready clip — preloaded by a hidden <video>.
  let nextClipUrl: string | undefined;
  for (let step = 1; step < rooms.length; step++) {
    const upcoming = rooms[(index + step) % rooms.length];
    if (upcoming.clip?.status === 'ready' && upcoming.clip.url) {
      nextClipUrl = upcoming.clip.url;
      break;
    }
  }

  const select = (h: TourHotspot | null) => setActive(h);
  const goTo = useCallback((i: number) => {
    setActive(null);
    go(i);
  }, [go]);
  const goNext = useCallback(() => {
    setActive(null);
    next();
  }, [next]);
  const goPrev = useCallback(() => {
    setActive(null);
    prev();
  }, [prev]);

  // Keyboard controls.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        if (active) setActive(null);
        else onClose();
      } else if (e.key === 'ArrowRight') goNext();
      else if (e.key === 'ArrowLeft') goPrev();
      else if (e.key === ' ') {
        e.preventDefault();
        togglePlaying();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [active, onClose, goNext, goPrev, togglePlaying]);

  // Swipe left/right to change rooms (pointer events cover touch + mouse).
  const downRef = useRef<{ x: number; y: number } | null>(null);
  const swipedRef = useRef(false);
  const onPointerDown = (e: React.PointerEvent) => {
    downRef.current = { x: e.clientX, y: e.clientY };
  };
  const onPointerUp = (e: React.PointerEvent) => {
    const down = downRef.current;
    downRef.current = null;
    if (!down) return;
    const dx = e.clientX - down.x;
    const dy = e.clientY - down.y;
    if (Math.abs(dx) > SWIPE_MIN_PX && Math.abs(dx) > Math.abs(dy) * 1.5) {
      // Suppress the tap-zone click that follows a swipe release.
      swipedRef.current = true;
      setTimeout(() => { swipedRef.current = false; }, 0);
      if (dx < 0) goNext();
      else goPrev();
    }
  };
  const tap = (action: () => void) => () => {
    if (!swipedRef.current) action();
  };

  return (
    <div className="flex w-full flex-1 justify-center overflow-hidden sm:items-center sm:p-4">
      {/* Vertical 9:16 Shorts frame */}
      <div
        className="relative h-full w-full touch-pan-y overflow-hidden bg-black sm:h-[92vh] sm:aspect-[9/16] sm:w-auto sm:rounded-3xl sm:shadow-2xl"
        onPointerDown={onPointerDown}
        onPointerUp={onPointerUp}
      >
        <RoomScene
          key={room.id}
          room={room}
          muted={muted}
          active={active}
          onSelect={select}
          videoRef={setVideoEl}
          onClipEnded={goNext}
        />
        {nextClipUrl && (
          // Warm the next room's clip so the transition is seamless.
          <video src={nextClipUrl} preload="auto" muted playsInline aria-hidden="true" className="hidden" />
        )}

        {/* Tap zones (below hotspots & overlays): left third back, rest forward. */}
        <button
          type="button"
          aria-hidden="true"
          onClick={tap(goPrev)}
          className="absolute inset-y-0 left-0 z-10 w-1/3 cursor-default"
          tabIndex={-1}
        />
        <button
          type="button"
          aria-hidden="true"
          onClick={tap(goNext)}
          className="absolute inset-y-0 right-0 z-10 w-2/3 cursor-default"
          tabIndex={-1}
        />

        {/* Top overlay: segmented progress + header */}
        <div className="pointer-events-none absolute inset-x-0 top-0 z-30 bg-gradient-to-b from-black/70 to-transparent px-3 pb-10 pt-3">
          <div className="pointer-events-auto flex gap-1">
            {rooms.map((r, i) => (
              <div key={r.id} className="h-1 flex-1 overflow-hidden rounded-full bg-white/25">
                <div
                  className="h-full rounded-full bg-white"
                  style={{ width: i < index ? '100%' : i === index ? `${progress * 100}%` : '0%' }}
                />
              </div>
            ))}
          </div>
          <div className="pointer-events-auto mt-3 flex items-center justify-between gap-2">
            <div className="min-w-0">
              <p className="truncate text-sm font-semibold text-white">{tour.title}</p>
              <p className="text-xs text-white/70">
                Virtual tour · {index + 1}/{rooms.length}
              </p>
            </div>
            {modeToggle}
            <button
              onClick={onClose}
              aria-label="Close tour"
              className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-white/15 text-white hover:bg-white/25"
            >
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
                <path d="M18 6 6 18M6 6l12 12" />
              </svg>
            </button>
          </div>
        </div>

        {/* Right action rail */}
        <div className="absolute right-3 top-1/2 z-30 flex -translate-y-1/2 flex-col gap-3">
          <RailButton label={playing ? 'Pause' : 'Play'} onClick={togglePlaying}>
            {playing ? <PauseIcon size={20} /> : <PlayIcon size={20} />}
          </RailButton>
          <RailButton label={muted ? 'Unmute' : 'Mute'} onClick={onToggleMuted}>
            {muted ? <VolumeMuteIcon size={20} /> : <VolumeIcon size={20} />}
          </RailButton>
          <RailButton label="Previous room" onClick={goPrev}>
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="m18 15-6-6-6 6" /></svg>
          </RailButton>
          <RailButton label="Next room" onClick={goNext}>
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="m6 9 6 6 6-6" /></svg>
          </RailButton>
        </div>

        {/* Bottom overlay: caption + filmstrip */}
        <div className="absolute inset-x-0 bottom-0 z-30 bg-gradient-to-t from-black/85 via-black/40 to-transparent px-4 pb-4 pt-16">
          <span className="inline-block rounded-full bg-white/15 px-3 py-1 text-[11px] font-semibold uppercase tracking-wide text-white backdrop-blur">
            {room.area}
          </span>
          {room.clip?.status === 'pending' && (
            <span className="ml-2 inline-block rounded-full bg-white/15 px-3 py-1 text-[11px] font-semibold text-white/85 backdrop-blur">
              Video walkthrough generating…
            </span>
          )}
          <h2 className="mt-2 text-2xl font-bold text-white drop-shadow">{room.name}</h2>
          <p className="text-sm text-white/85">{room.caption}</p>
          {room.dimensions && <p className="mt-0.5 text-xs text-white/65">Approx. {room.dimensions}</p>}

          <div className="mt-3 flex gap-2 overflow-x-auto pb-1">
            {rooms.map((r, i) => (
              <button
                key={r.id}
                onClick={() => goTo(i)}
                className={`shrink-0 rounded-full border px-3 py-1.5 text-xs font-medium transition-colors ${
                  i === index
                    ? 'border-white bg-white text-black'
                    : 'border-white/30 text-white/75 hover:bg-white/10'
                }`}
              >
                {i + 1}. {r.name}
              </button>
            ))}
          </div>
        </div>

        {/* Hotspot detail panel */}
        {active && <HotspotDetail hotspot={active} onClose={() => setActive(null)} />}
      </div>
    </div>
  );
}

function RailButton({
  label,
  onClick,
  children,
}: {
  label: string;
  onClick: () => void;
  children: ReactNode;
}) {
  return (
    <button
      onClick={onClick}
      aria-label={label}
      className="flex h-11 w-11 items-center justify-center rounded-full bg-white/15 text-white backdrop-blur transition-colors hover:bg-white/30"
    >
      {children}
    </button>
  );
}
