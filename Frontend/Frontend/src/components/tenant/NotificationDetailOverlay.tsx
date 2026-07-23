import { useEffect, useState } from 'react';
import type { Notification, NotificationType } from '../../types';
import {
  CalendarIcon, CardIcon, ToolIcon, MessageIcon, ShieldIcon, XIcon, PlusIcon, MinusIcon,
} from './icons';

const ICONS: Record<NotificationType, React.ReactNode> = {
  booking: <CalendarIcon size={20} />,
  payment: <CardIcon size={20} />,
  maintenance: <ToolIcon size={20} />,
  message: <MessageIcon size={20} />,
  safety: <ShieldIcon size={20} />,
};

const MIN_FONT_SIZE = 14;
const MAX_FONT_SIZE = 32;
const FONT_STEP = 2;
const DEFAULT_FONT_SIZE = 18;

interface NotificationDetailOverlayProps {
  notification: Notification;
  onClose: () => void;
}

/** Full-screen notification reader with a +/- text-size control for low-vision users. */
export default function NotificationDetailOverlay({ notification, onClose }: NotificationDetailOverlayProps) {
  const [fontSize, setFontSize] = useState(DEFAULT_FONT_SIZE);

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [onClose]);

  const shrink = () => setFontSize((s) => Math.max(MIN_FONT_SIZE, s - FONT_STEP));
  const grow = () => setFontSize((s) => Math.min(MAX_FONT_SIZE, s + FONT_STEP));

  return (
    <div role="dialog" aria-modal="true" aria-label={notification.title} className="fixed inset-0 z-[100] flex flex-col bg-white">
      <header className="flex items-center justify-between border-b border-gray-100 px-5 py-4 sm:px-8">
        <button onClick={onClose} aria-label="Close" className="flex h-10 w-10 items-center justify-center rounded-full text-ink hover:bg-gray-100">
          <XIcon size={20} />
        </button>

        <div className="flex items-center gap-1 rounded-full border border-gray-200 px-1 py-1">
          <button
            onClick={shrink}
            disabled={fontSize <= MIN_FONT_SIZE}
            aria-label="Decrease text size"
            className="flex h-8 w-8 items-center justify-center rounded-full text-ink hover:bg-gray-100 disabled:opacity-30"
          >
            <MinusIcon size={16} />
          </button>
          <span className="w-10 text-center text-xs font-semibold text-muted">Aa</span>
          <button
            onClick={grow}
            disabled={fontSize >= MAX_FONT_SIZE}
            aria-label="Increase text size"
            className="flex h-8 w-8 items-center justify-center rounded-full text-ink hover:bg-gray-100 disabled:opacity-30"
          >
            <PlusIcon size={16} />
          </button>
        </div>
      </header>

      <div className="mx-auto w-full max-w-2xl flex-1 overflow-y-auto px-5 py-8 sm:px-8">
        <div className="mb-4 flex items-center gap-3">
          <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-lg bg-brand-50 text-brand">
            {ICONS[notification.type]}
          </span>
          <span className="text-sm text-muted">{notification.time}</span>
        </div>
        <h1 style={{ fontSize: fontSize * 1.4 }} className="mb-4 font-bold text-ink">
          {notification.title}
        </h1>
        <p style={{ fontSize, lineHeight: 1.7 }} className="text-ink">
          {notification.body}
        </p>
      </div>
    </div>
  );
}
