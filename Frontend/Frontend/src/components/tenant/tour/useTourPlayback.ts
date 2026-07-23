import { useCallback, useEffect, useRef, useState } from 'react';

/** How long a room without a video clip stays on screen. */
export const AUTOPLAY_MS = 4200;

export interface TourPlayback {
  index: number;
  playing: boolean;
  /** 0–1 progress through the current room (clip time or autoplay timer). */
  progress: number;
  go: (index: number) => void;
  next: () => void;
  prev: () => void;
  togglePlaying: () => void;
  /** Callback ref for the current room's <video>, when it has one. */
  setVideoEl: (el: HTMLVideoElement | null) => void;
}

/**
 * Rooms-mode playback engine. Rooms with a video clip advance when the clip
 * ends (the scene fires `next` via onEnded) and report real playback progress;
 * rooms without one run a pausable AUTOPLAY_MS timer. `suspended` (e.g. an
 * open hotspot) pauses without losing position, unlike the old CSS-animation
 * progress bar which restarted from zero on every resume.
 */
export function useTourPlayback(roomCount: number, suspended: boolean): TourPlayback {
  const [index, setIndex] = useState(0);
  const [playing, setPlaying] = useState(true);
  const [progress, setProgress] = useState(0);

  const videoRef = useRef<HTMLVideoElement | null>(null);
  // Timer-room bookkeeping: accumulated elapsed ms + start of the current run.
  const elapsedRef = useRef(0);
  const startedAtRef = useRef<number | null>(null);

  const go = useCallback((target: number) => {
    elapsedRef.current = 0;
    startedAtRef.current = null;
    setProgress(0);
    setIndex(target);
  }, []);
  const next = useCallback(
    () => go((index + 1) % roomCount),
    [go, index, roomCount],
  );
  const prev = useCallback(
    () => go((index - 1 + roomCount) % roomCount),
    [go, index, roomCount],
  );
  const togglePlaying = useCallback(() => setPlaying((p) => !p), []);

  const setVideoEl = useCallback((el: HTMLVideoElement | null) => {
    const hadVideo = videoRef.current !== null;
    videoRef.current = el;
    // A clip that errors out mid-room unmounts its <video>; hand the room to
    // the autoplay timer so the tour keeps advancing instead of stalling.
    if (el === null && hadVideo && startedAtRef.current === null) {
      startedAtRef.current = performance.now();
    }
  }, []);

  useEffect(() => {
    const active = playing && !suspended;
    const video = videoRef.current;

    if (!active) {
      video?.pause();
      if (startedAtRef.current != null) {
        elapsedRef.current += performance.now() - startedAtRef.current;
        startedAtRef.current = null;
      }
      return;
    }

    if (video) {
      video.play().catch(() => {});
    } else {
      startedAtRef.current = performance.now();
    }

    let raf = 0;
    let advanced = false;
    const tick = () => {
      const v = videoRef.current;
      if (v) {
        // MediaRecorder WebM can report Infinity — fall back to the nominal
        // 8s clip length so the progress bar still tracks.
        const duration = Number.isFinite(v.duration) && v.duration > 0 ? v.duration : 8;
        setProgress(Math.min(v.currentTime / duration, 1));
      } else {
        const elapsed =
          elapsedRef.current +
          (startedAtRef.current != null ? performance.now() - startedAtRef.current : 0);
        if (elapsed >= AUTOPLAY_MS) {
          if (!advanced) {
            advanced = true;
            go((index + 1) % roomCount);
          }
          return;
        }
        setProgress(elapsed / AUTOPLAY_MS);
      }
      raf = requestAnimationFrame(tick);
    };
    raf = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf);
  }, [playing, suspended, index, roomCount, go]);

  return { index, playing, progress, go, next, prev, togglePlaying, setVideoEl };
}
