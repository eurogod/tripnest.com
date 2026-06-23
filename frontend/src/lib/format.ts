import { StayType } from './enums';
import type { Property } from '@/types/api';

const GHS = new Intl.NumberFormat('en-GH', {
  style: 'currency',
  currency: 'GHS',
  maximumFractionDigits: 0,
});

export const money = (v: number | null | undefined) => (v == null ? '—' : GHS.format(v));

export function priceLabel(p: Property): { amount: number; unit: string } {
  if (p.stayType === StayType.ShortTerm && p.dailyRate) {
    return { amount: p.dailyRate, unit: '/ night' };
  }
  return { amount: p.monthlyRent, unit: p.stayType === StayType.Student ? '/ semester' : '/ month' };
}

export const fmtDate = (iso?: string | null) =>
  iso ? new Date(iso).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' }) : '—';

export const fmtDateTime = (iso?: string | null) =>
  iso ? new Date(iso).toLocaleString('en-GB', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }) : '—';

export function relativeTime(iso?: string | null): string {
  if (!iso) return '';
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.round(diff / 60000);
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.round(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  const days = Math.round(hrs / 24);
  if (days < 7) return `${days}d ago`;
  return fmtDate(iso);
}

/** Photos are stored as a comma-separated (or JSON array) string of /uploads paths. */
export function parsePhotos(raw?: string | null): string[] {
  if (!raw) return [];
  const trimmed = raw.trim();
  if (trimmed.startsWith('[')) {
    try {
      const arr = JSON.parse(trimmed);
      if (Array.isArray(arr)) return arr.filter(Boolean).map(normalizeUpload);
    } catch {
      /* fall through to CSV */
    }
  }
  return trimmed
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean)
    .map(normalizeUpload);
}

function normalizeUpload(path: string): string {
  if (/^https?:\/\//.test(path)) return path;
  return path.startsWith('/') ? path : `/${path}`;
}

export const parseAmenities = (raw?: string | null): string[] =>
  raw
    ? raw
        .split(',')
        .map((s) => s.trim())
        .filter(Boolean)
    : [];

/** Deterministic stock photo so listings without uploads still look real (Unsplash). */
export function fallbackPhoto(seed: string): string {
  const photos = [
    'photo-1568605114967-8130f3a36994',
    'photo-1512917774080-9991f1c4c750',
    'photo-1502672260266-1c1ef2d93688',
    'photo-1493809842364-78817add7ffb',
    'photo-1560448204-e02f11c3d0e2',
    'photo-1522708323590-d24dbb6b0267',
    'photo-1484154218962-a197022b5858',
    'photo-1567496898669-ee935f5f647a',
  ];
  let h = 0;
  for (let i = 0; i < seed.length; i++) h = (h * 31 + seed.charCodeAt(i)) >>> 0;
  return `https://images.unsplash.com/${photos[h % photos.length]}?auto=format&fit=crop&w=900&q=70`;
}

export function propertyPhoto(p: Property): string {
  const photos = parsePhotos(p.photoPaths);
  return photos[0] ?? fallbackPhoto(p.propertyId);
}

export function initials(name: string): string {
  return name
    .split(/\s+/)
    .slice(0, 2)
    .map((s) => s[0]?.toUpperCase() ?? '')
    .join('');
}

export function avatarColor(seed: string): string {
  const colors = ['#0f766e', '#b8901f', '#7c3aed', '#dc2626', '#2563eb', '#db2777', '#0891b2'];
  let h = 0;
  for (let i = 0; i < seed.length; i++) h = (h * 31 + seed.charCodeAt(i)) >>> 0;
  return colors[h % colors.length];
}

export function nights(checkIn: string, checkOut: string): number {
  const ms = new Date(checkOut).getTime() - new Date(checkIn).getTime();
  return Math.max(0, Math.round(ms / 86400000));
}
