import type { TourHotspot } from '../../../types';
import CategoryIcon from './CategoryIcon';

export default function HotspotDetail({ hotspot, onClose }: { hotspot: TourHotspot; onClose: () => void }) {
  return (
    <div className="absolute inset-x-3 bottom-24 z-40 mx-auto max-w-sm rounded-2xl bg-white p-4 text-ink shadow-2xl animate-[tour-fade_250ms_ease-out] motion-reduce:animate-none">
      <div className="flex items-start gap-3">
        <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-brand-50 text-brand">
          <CategoryIcon category={hotspot.category} size={20} />
        </span>
        <div className="min-w-0 flex-1">
          <p className="font-semibold">{hotspot.label}</p>
          <p className="mt-1 text-sm text-muted">{hotspot.detail}</p>
        </div>
        <button onClick={onClose} aria-label="Close detail" className="text-muted hover:text-ink">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"><path d="M18 6 6 18M6 6l12 12" /></svg>
        </button>
      </div>
    </div>
  );
}
