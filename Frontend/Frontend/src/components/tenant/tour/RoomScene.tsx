import { useState } from 'react';
import type { TourHotspot, TourRoom } from '../../../types';
import CategoryIcon from './CategoryIcon';

interface RoomSceneProps {
  room: TourRoom;
  muted: boolean;
  active: TourHotspot | null;
  onSelect: (h: TourHotspot) => void;
  videoRef: (el: HTMLVideoElement | null) => void;
  onClipEnded: () => void;
}

/**
 * One room of the walkthrough. Media precedence: ready generated clip →
 * still image → gradient placeholder with Ken Burns motion. Pending/failed
 * clips render exactly like rooms without one.
 */
export default function RoomScene({
  room, muted, active, onSelect, videoRef, onClipEnded,
}: RoomSceneProps) {
  // Local buffering/failure state; resets naturally because RoomsPlayer
  // remounts the scene per room via key={room.id}.
  const [buffering, setBuffering] = useState(false);
  const [failed, setFailed] = useState(false);
  const clip =
    room.clip?.status === 'ready' && room.clip.url && !failed ? room.clip : undefined;

  return (
    <div className="absolute inset-0 animate-[tour-fade_700ms_ease-out] motion-reduce:animate-none">
      {clip ? (
        <video
          ref={videoRef}
          src={clip.url}
          poster={clip.poster}
          muted={muted}
          playsInline
          preload="auto"
          onEnded={onClipEnded}
          onWaiting={() => setBuffering(true)}
          onStalled={() => setBuffering(true)}
          onPlaying={() => setBuffering(false)}
          onCanPlay={() => setBuffering(false)}
          onError={() => setFailed(true)} // fall back to image/gradient
          className="absolute inset-0 h-full w-full object-cover"
        />
      ) : room.image ? (
        <img src={room.image} alt={room.name} className="absolute inset-0 h-full w-full object-cover" />
      ) : (
        <div
          className="absolute inset-0 origin-center animate-[tour-kenburns_16s_ease-in-out_infinite_alternate] motion-reduce:animate-none"
          style={{ backgroundImage: `linear-gradient(135deg, ${room.from}, ${room.to})` }}
        />
      )}
      {clip && buffering && (
        <span className="pointer-events-none absolute left-1/2 top-1/2 z-10 h-10 w-10 -translate-x-1/2 -translate-y-1/2 animate-spin rounded-full border-2 border-white/30 border-t-white" />
      )}
      {/* Hotspots (above the tap zones, below the overlays) */}
      {room.hotspots.map((h) => (
        <button
          key={h.id}
          onClick={() => onSelect(h)}
          aria-label={h.label}
          className="group absolute z-20 -translate-x-1/2 -translate-y-1/2"
          style={{ left: `${h.x}%`, top: `${h.y}%` }}
        >
          <span className="absolute inset-0 -m-2 rounded-full bg-white/40 animate-ping motion-reduce:animate-none" />
          <span
            className={`relative flex h-9 w-9 items-center justify-center rounded-full border-2 shadow-lg transition-transform group-hover:scale-110 ${
              active?.id === h.id ? 'border-white bg-brand text-white' : 'border-white bg-white/90 text-brand'
            }`}
          >
            <CategoryIcon category={h.category} />
          </span>
        </button>
      ))}
    </div>
  );
}
