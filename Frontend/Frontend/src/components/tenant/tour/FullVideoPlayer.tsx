import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import type { PropertyTour, TourSegment } from '../../../types';
import { PlayIcon, PauseIcon, VolumeIcon, VolumeMuteIcon } from '../icons';

const SEEK_STEP_SEC = 10;

function formatTime(sec: number): string {
  const s = Math.max(0, Math.floor(sec));
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
}

interface FullVideoPlayerProps {
  tour: PropertyTour;
  onClose: () => void;
  muted: boolean;
  onToggleMuted: () => void;
  modeToggle?: ReactNode;
}

/**
 * Continuous walkthrough with per-room chapters and seeking. Plays either a
 * single authored video or a synthesized playlist of generated room clips —
 * a single URL is just a one-segment playlist, so both share one timeline
 * (global time = segment offset + element time).
 */
export default function FullVideoPlayer({
  tour, onClose, muted, onToggleMuted, modeToggle,
}: FullVideoPlayerProps) {
  const full = tour.fullVideo!;
  const segments: TourSegment[] = useMemo(
    () => full.segments ?? [{
      roomId: full.chapters[0]?.roomId ?? '',
      url: full.url!,
      durationSec: full.durationSec ?? 0,
    }],
    [full],
  );

  const videoRef = useRef<HTMLVideoElement | null>(null);
  const [segIndex, setSegIndex] = useState(0);
  const [segDurations, setSegDurations] = useState<number[]>(
    () => segments.map((s) => s.durationSec),
  );
  const [playing, setPlaying] = useState(true);
  const [ended, setEnded] = useState(false);
  const [buffering, setBuffering] = useState(false);
  const [time, setTime] = useState(0);
  // Local time to apply once a newly loaded segment has metadata.
  const pendingSeekRef = useRef<number | null>(null);
  const timeRef = useRef(0);

  const offsets = useMemo(() => {
    const out: number[] = [];
    let acc = 0;
    for (const d of segDurations) {
      out.push(acc);
      acc += d;
    }
    return out;
  }, [segDurations]);
  const totalDuration = (offsets[segments.length - 1] ?? 0) + (segDurations[segments.length - 1] ?? 0);

  // Synthesized playlists map chapters 1:1 onto segments, so pin their
  // start times to the (runtime-corrected) segment offsets.
  const chapters = useMemo(
    () => (full.segments
      ? full.chapters.map((c, i) => ({ ...c, startSec: offsets[i] ?? c.startSec }))
      : full.chapters),
    [full, offsets],
  );
  const current = chapters.reduce<typeof chapters[number] | undefined>(
    (acc, c) => (c.startSec <= time ? c : acc),
    undefined,
  );
  const currentRoom = current && tour.rooms.find((r) => r.id === current.roomId);

  const goToSegment = useCallback((index: number, localTime: number) => {
    pendingSeekRef.current = localTime;
    setEnded(false);
    setSegIndex(index);
  }, []);

  const togglePlaying = useCallback(() => {
    if (ended) {
      if (segIndex === 0 && videoRef.current) videoRef.current.currentTime = 0;
      else goToSegment(0, 0);
      setEnded(false);
      setPlaying(true);
      return;
    }
    setPlaying((p) => !p);
  }, [ended, segIndex, goToSegment]);

  const seek = useCallback((globalTo: number) => {
    const to = Math.min(Math.max(globalTo, 0), totalDuration || globalTo);
    let target = 0;
    for (let j = 0; j < segments.length; j++) {
      if (offsets[j] <= to) target = j;
    }
    const local = to - offsets[target];
    if (target === segIndex && videoRef.current) {
      videoRef.current.currentTime = local;
      setTime(to);
      setEnded(false);
    } else {
      goToSegment(target, local);
    }
  }, [segments.length, offsets, totalDuration, segIndex, goToSegment]);

  const onEnded = useCallback(() => {
    if (segIndex < segments.length - 1) {
      goToSegment(segIndex + 1, 0);
    } else {
      setEnded(true);
      setPlaying(false);
    }
  }, [segIndex, segments.length, goToSegment]);

  const onLoadedMetadata = useCallback(() => {
    const v = videoRef.current;
    if (!v) return;
    if (Number.isFinite(v.duration) && v.duration > 0) {
      setSegDurations((prev) => {
        if (Math.abs((prev[segIndex] ?? 0) - v.duration) <= 0.25) return prev;
        const next = [...prev];
        next[segIndex] = v.duration;
        return next;
      });
    }
    if (pendingSeekRef.current != null) {
      v.currentTime = pendingSeekRef.current;
      pendingSeekRef.current = null;
    }
  }, [segIndex]);

  // Drive the element from state and keep the scrubber in sync via rAF.
  useEffect(() => {
    const v = videoRef.current;
    if (!v) return;
    if (playing && !ended) v.play().catch(() => {});
    else v.pause();

    let raf = 0;
    const tick = () => {
      const globalTime = (offsets[segIndex] ?? 0) + v.currentTime;
      timeRef.current = globalTime;
      setTime(globalTime);
      raf = requestAnimationFrame(tick);
    };
    raf = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf);
  }, [playing, ended, segIndex, offsets]);

  // Keyboard controls.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
      else if (e.key === 'ArrowRight') seek(timeRef.current + SEEK_STEP_SEC);
      else if (e.key === 'ArrowLeft') seek(timeRef.current - SEEK_STEP_SEC);
      else if (e.key === ' ') {
        e.preventDefault();
        togglePlaying();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose, seek, togglePlaying]);

  const nextSegmentUrl = segments[segIndex + 1]?.url;

  return (
    <div className="flex w-full flex-1 flex-col overflow-hidden p-4 sm:p-6">
      {/* Header */}
      <div className="flex items-center justify-between gap-2 pb-3">
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold text-white">{tour.title}</p>
          <p className="text-xs text-white/70">
            Full walkthrough{currentRoom ? ` · ${currentRoom.name}` : ''}
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

      {/* Stage */}
      <div className="relative flex min-h-0 flex-1 items-center justify-center overflow-hidden rounded-2xl bg-black">
        <video
          ref={videoRef}
          src={segments[segIndex].url}
          poster={segIndex === 0 ? full.poster : undefined}
          muted={muted}
          playsInline
          preload="auto"
          onEnded={onEnded}
          onLoadedMetadata={onLoadedMetadata}
          onWaiting={() => setBuffering(true)}
          onStalled={() => setBuffering(true)}
          onPlaying={() => setBuffering(false)}
          onCanPlay={() => setBuffering(false)}
          onError={onEnded} // treat a dead segment like a finished one: advance or end
          onClick={togglePlaying}
          className="h-full w-full object-contain"
        />
        {nextSegmentUrl && (
          // Warm the next clip so segment handoff is seamless.
          <video src={nextSegmentUrl} preload="auto" muted playsInline aria-hidden="true" className="hidden" />
        )}
        {buffering && !ended && (
          <span className="pointer-events-none absolute h-10 w-10 animate-spin rounded-full border-2 border-white/30 border-t-white" />
        )}
        {(!playing || ended) && !buffering && (
          <button
            onClick={togglePlaying}
            aria-label={ended ? 'Replay walkthrough' : 'Play'}
            className="absolute flex h-16 w-16 items-center justify-center rounded-full bg-white/20 text-white backdrop-blur transition-colors hover:bg-white/30"
          >
            <PlayIcon size={28} />
          </button>
        )}
      </div>

      {/* Controls */}
      <div className="pt-3">
        <div className="relative">
          <input
            type="range"
            min={0}
            max={totalDuration || 0}
            step={0.1}
            value={Math.min(time, totalDuration || 0)}
            onInput={(e) => seek(Number(e.currentTarget.value))}
            aria-label="Seek walkthrough"
            className="h-1.5 w-full cursor-pointer appearance-none rounded-full bg-white/25 accent-white"
          />
          {/* Chapter tick marks */}
          {totalDuration > 0 && chapters.map((c) => (
            <span
              key={c.roomId}
              className="pointer-events-none absolute top-1/2 h-2.5 w-0.5 -translate-y-1/2 rounded-full bg-white/70"
              style={{ left: `${(c.startSec / totalDuration) * 100}%` }}
            />
          ))}
        </div>
        <div className="mt-2 flex items-center gap-3">
          <button
            onClick={togglePlaying}
            aria-label={ended ? 'Replay' : playing ? 'Pause' : 'Play'}
            className="flex h-10 w-10 items-center justify-center rounded-full bg-white/15 text-white hover:bg-white/25"
          >
            {playing && !ended ? <PauseIcon size={18} /> : <PlayIcon size={18} />}
          </button>
          <button
            onClick={onToggleMuted}
            aria-label={muted ? 'Unmute' : 'Mute'}
            className="flex h-10 w-10 items-center justify-center rounded-full bg-white/15 text-white hover:bg-white/25"
          >
            {muted ? <VolumeMuteIcon size={18} /> : <VolumeIcon size={18} />}
          </button>
          <span className="text-xs tabular-nums text-white/80">
            {formatTime(time)} / {formatTime(totalDuration)}
          </span>
        </div>

        {/* Chapter pills */}
        <div className="mt-3 flex gap-2 overflow-x-auto pb-1">
          {chapters.map((c) => {
            const room = tour.rooms.find((r) => r.id === c.roomId);
            if (!room) return null;
            const isCurrent = current?.roomId === c.roomId;
            return (
              <button
                key={c.roomId}
                onClick={() => { seek(c.startSec); setPlaying(true); }}
                className={`shrink-0 rounded-full border px-3 py-1.5 text-xs font-medium transition-colors ${
                  isCurrent
                    ? 'border-white bg-white text-black'
                    : 'border-white/30 text-white/75 hover:bg-white/10'
                }`}
              >
                {formatTime(c.startSec)} · {room.name}
              </button>
            );
          })}
        </div>
      </div>
    </div>
  );
}
