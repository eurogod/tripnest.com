import { useCallback, useEffect, useState } from 'react';
import Button from './ui/Button';

export interface TourStep {
  /** CSS selector of the element to spotlight; omit for a centered card. */
  selector?: string;
  title: string;
  body: string;
}

interface GuidedTourProps {
  steps: TourStep[];
  /** localStorage key that marks the tour as seen; the tour renders only while unset. */
  storageKey: string;
}

interface Anchor {
  top: number;
  left: number;
  width: number;
  height: number;
}

function seen(key: string): boolean {
  try { return localStorage.getItem(key) !== null; } catch { return true; }
}

function markSeen(key: string): void {
  try { localStorage.setItem(key, new Date().toISOString()); } catch { /* private mode */ }
}

/**
 * One-time spotlight walkthrough for first-time users: dims the app, rings
 * the highlighted element, and steps through short blurbs. Skippable at any
 * point; never shows again once finished or skipped.
 */
export default function GuidedTour({ steps, storageKey }: GuidedTourProps) {
  const [open, setOpen] = useState(() => !seen(storageKey));
  const [index, setIndex] = useState(0);
  const [anchor, setAnchor] = useState<Anchor | null>(null);

  const step = steps[index];

  const locate = useCallback(() => {
    if (!step?.selector) {
      setAnchor(null);
      return;
    }
    const el = document.querySelector(step.selector);
    const rect = el?.getBoundingClientRect();
    // Hidden targets (collapsed sidebar on mobile) fall back to a centered card.
    if (!rect || rect.width === 0 || rect.height === 0) {
      setAnchor(null);
      return;
    }
    el?.scrollIntoView({ block: 'nearest' });
    setAnchor({ top: rect.top, left: rect.left, width: rect.width, height: rect.height });
  }, [step]);

  useEffect(() => {
    if (!open) return;
    // Measure after paint so the sidebar has its final geometry.
    const raf = requestAnimationFrame(locate);
    window.addEventListener('resize', locate);
    return () => {
      cancelAnimationFrame(raf);
      window.removeEventListener('resize', locate);
    };
  }, [open, locate]);

  if (!open || !step) return null;

  const finish = () => {
    markSeen(storageKey);
    setOpen(false);
  };

  const next = () => {
    if (index + 1 >= steps.length) finish();
    else setIndex(index + 1);
  };

  const PAD = 6;
  const popoverTop = anchor
    ? Math.min(window.innerHeight - 220, anchor.top + anchor.height + PAD + 8)
    : undefined;
  const popoverLeft = anchor
    ? Math.max(16, Math.min(window.innerWidth - 336, anchor.left))
    : undefined;

  return (
    <div className="fixed inset-0 z-[70]" role="dialog" aria-modal="true" aria-label="Getting started tour">
      {/* Backdrop, or a spotlight ring cut out of it around the target. */}
      {anchor ? (
        <div
          className="absolute rounded-xl ring-2 ring-brand transition-all duration-200"
          style={{
            top: anchor.top - PAD,
            left: anchor.left - PAD,
            width: anchor.width + PAD * 2,
            height: anchor.height + PAD * 2,
            boxShadow: '0 0 0 9999px rgba(15, 23, 42, 0.55)',
          }}
        />
      ) : (
        <div className="absolute inset-0 bg-slate-900/55" />
      )}

      <div
        className={`absolute w-80 rounded-2xl bg-white p-5 shadow-2xl ${anchor ? '' : 'left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2'}`}
        style={anchor ? { top: popoverTop, left: popoverLeft } : undefined}
      >
        <p className="text-xs font-semibold uppercase tracking-wide text-brand">
          {index + 1} of {steps.length}
        </p>
        <h2 className="mt-1 text-lg font-bold text-ink">{step.title}</h2>
        <p className="mt-1.5 text-sm leading-relaxed text-muted">{step.body}</p>

        <div className="mt-4 flex items-center justify-between gap-3">
          <span className="flex gap-1.5">
            {steps.map((_, i) => (
              <span
                key={i}
                className={`h-1.5 w-1.5 rounded-full ${i === index ? 'bg-brand' : 'bg-gray-200'}`}
              />
            ))}
          </span>
          <span className="flex gap-2">
            <Button variant="ghost" size="sm" onClick={finish}>Skip</Button>
            <Button size="sm" onClick={next}>
              {index + 1 >= steps.length ? 'Done' : 'Next'}
            </Button>
          </span>
        </div>
      </div>
    </div>
  );
}
