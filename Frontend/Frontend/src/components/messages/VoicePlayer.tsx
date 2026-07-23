import { useRef, useState } from 'react';
import { PlayIcon, PauseIcon } from '../tenant/icons';
import { clock } from './time';

/** Inline player for a sent voice message — play/pause + progress + duration. */
export default function VoicePlayer({ url, duration, mine }: { url: string; duration: number; mine: boolean }) {
  const audioRef = useRef<HTMLAudioElement>(null);
  const [playing, setPlaying] = useState(false);
  const [elapsed, setElapsed] = useState(0);

  const toggle = () => {
    const audio = audioRef.current;
    if (!audio) return;
    if (playing) audio.pause();
    else void audio.play();
  };

  const progress = duration > 0 ? Math.min(1, elapsed / duration) : 0;

  return (
    <div className="flex items-center gap-2.5 py-0.5">
      <button
        type="button"
        onClick={toggle}
        aria-label={playing ? 'Pause voice message' : 'Play voice message'}
        className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-full ${
          mine ? 'bg-white/20 text-white hover:bg-white/30' : 'bg-brand-50 text-brand hover:bg-brand-50/70'
        }`}
      >
        {playing ? <PauseIcon size={15} /> : <PlayIcon size={15} />}
      </button>
      <div className={`h-1 w-28 overflow-hidden rounded-full ${mine ? 'bg-white/30' : 'bg-gray-200'}`}>
        <div
          className={`h-full rounded-full ${mine ? 'bg-white' : 'bg-brand'}`}
          style={{ width: `${progress * 100}%` }}
        />
      </div>
      <span className={`text-[11px] tabular-nums ${mine ? 'text-white/80' : 'text-muted'}`}>
        {clock(playing || elapsed > 0 ? elapsed : duration)}
      </span>
      <audio
        ref={audioRef}
        src={url}
        preload="metadata"
        onPlay={() => setPlaying(true)}
        onPause={() => setPlaying(false)}
        onTimeUpdate={(e) => setElapsed(e.currentTarget.currentTime)}
        onEnded={() => { setPlaying(false); setElapsed(0); }}
      />
    </div>
  );
}
