import type { HotspotCategory } from '../../../types';

const PATHS: Record<HotspotCategory, React.ReactNode> = {
  bed: <><path d="M3 18v-6a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v6" /><path d="M3 18h18M3 14h18" /><path d="M7 10V8a1 1 0 0 1 1-1h3v3" /></>,
  seating: <><path d="M5 11V8a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2v3" /><path d="M3 13a2 2 0 0 1 4 0v3h10v-3a2 2 0 0 1 4 0v5H3z" /></>,
  kitchen: <><rect x="4" y="3" width="16" height="18" rx="2" /><path d="M4 11h16" /><path d="M8 6v1M8 15v3" /></>,
  bathroom: <><path d="M12 3v9" /><path d="M5 12h14v3a4 4 0 0 1-4 4H9a4 4 0 0 1-4-4z" /><path d="M9 21l-1 1M16 21l1 1" /></>,
  storage: <><rect x="3" y="4" width="18" height="16" rx="1" /><path d="M3 10h18M9 4v6M15 4v6" /></>,
  entertainment: <><rect x="2" y="4" width="20" height="13" rx="2" /><path d="M8 21h8M12 17v4" /></>,
  view: <><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7z" /><circle cx="12" cy="12" r="2.5" /></>,
  outdoor: <><circle cx="12" cy="9" r="4" /><path d="M12 13v8M8 17h8" /><path d="M5 21h14" /></>,
  amenity: <><path d="M12 3l1.8 4.6L18 9l-4.2 1.4L12 15l-1.8-4.6L6 9l4.2-1.4z" /></>,
  workspace: <><rect x="3" y="4" width="18" height="11" rx="1" /><path d="M2 19h20M9 15v4M15 15v4" /></>,
  parking: <><path d="M5 17v-5l1.5-4h11L19 12v5" /><circle cx="7.5" cy="17" r="1.5" /><circle cx="16.5" cy="17" r="1.5" /></>,
};

/** Compact line icon for a hotspot category. */
export default function CategoryIcon({ category, size = 15 }: { category: HotspotCategory; size?: number }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.8}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      {PATHS[category]}
    </svg>
  );
}
