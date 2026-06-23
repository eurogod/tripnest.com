import { Shield, Star } from './icons';
import { avatarColor, initials } from '@/lib/format';
import {
  AgreementStatusLabel,
  BookingStatusLabel,
  EscrowStatus,
  EscrowStatusLabel,
  MaintenanceStatusLabel,
  PropertyStatusLabel,
  WalkthroughStatusLabel,
} from '@/lib/enums';

/* ---------- Verified badge (the signature trust element) ---------- */
export function VerifiedBadge({ size = 'md', label = 'Verified' }: { size?: 'sm' | 'md'; label?: string }) {
  return (
    <span
      className={`pill bg-brand-50 text-brand-700 ring-1 ring-brand-600/20 ${
        size === 'sm' ? 'px-2 py-0.5 text-[11px]' : ''
      }`}
    >
      <Shield className={size === 'sm' ? 'h-3 w-3' : 'h-3.5 w-3.5'} />
      {label}
    </span>
  );
}

/* ---------- Star rating ---------- */
export function StarRating({ value, count, className = '' }: { value: number; count?: number; className?: string }) {
  return (
    <span className={`inline-flex items-center gap-1 text-sm font-semibold text-ink ${className}`}>
      <Star className="h-4 w-4 text-gold-500" />
      {value.toFixed(1)}
      {count != null && <span className="font-medium text-muted">({count})</span>}
    </span>
  );
}

/* ---------- Trust score chip (the "reality score") ---------- */
export function TrustChip({ score, label }: { score: number; label?: string }) {
  const tone =
    score >= 80 ? 'bg-success/10 text-success ring-success/25' : score >= 55 ? 'bg-gold-500/10 text-gold-700 ring-gold-600/25' : 'bg-danger/10 text-danger ring-danger/25';
  return (
    <span className={`pill ring-1 ${tone}`} title="TripNest reality score">
      <Shield className="h-3.5 w-3.5" />
      {Math.round(score)}
      {label ? <span className="font-medium opacity-80">· {label}</span> : null}
    </span>
  );
}

/* ---------- Generic status pill ---------- */
type Tone = 'ok' | 'warn' | 'bad' | 'info' | 'mut';
const toneClass: Record<Tone, string> = {
  ok: 'bg-success/10 text-success',
  warn: 'bg-warning/10 text-warning',
  bad: 'bg-danger/10 text-danger',
  info: 'bg-brand-50 text-brand-700',
  mut: 'bg-black/5 text-muted',
};
export function Pill({ tone = 'mut', children }: { tone?: Tone; children: React.ReactNode }) {
  return <span className={`pill ${toneClass[tone]}`}>{children}</span>;
}

export function BookingStatusPill({ status }: { status: number }) {
  const tone: Tone = status === 1 ? 'ok' : status === 4 ? 'bad' : status === 5 ? 'info' : 'warn';
  return <Pill tone={tone}>{BookingStatusLabel[status] ?? 'Unknown'}</Pill>;
}
export function EscrowStatusPill({ status }: { status: number }) {
  const tone: Tone =
    status === EscrowStatus.Released ? 'ok' : status === EscrowStatus.Disputed ? 'bad' : status === EscrowStatus.Refunded ? 'mut' : 'warn';
  return (
    <Pill tone={tone}>
      {status === EscrowStatus.HeldInEscrow ? '🔒 ' : ''}
      {EscrowStatusLabel[status] ?? 'Unknown'}
    </Pill>
  );
}
export function AgreementStatusPill({ status }: { status: number }) {
  const tone: Tone = status === 2 ? 'ok' : status === 1 ? 'warn' : 'mut';
  return <Pill tone={tone}>{AgreementStatusLabel[status] ?? 'Unknown'}</Pill>;
}
export function PropertyStatusPill({ status }: { status: number }) {
  const tone: Tone = status === 1 ? 'ok' : status === 0 ? 'warn' : 'mut';
  return <Pill tone={tone}>{PropertyStatusLabel[status] ?? 'Unknown'}</Pill>;
}
export function WalkthroughStatusPill({ status }: { status: number }) {
  const tone: Tone = status === 2 ? 'ok' : status === 1 ? 'warn' : status === 3 ? 'bad' : 'mut';
  return <Pill tone={tone}>{WalkthroughStatusLabel[status] ?? 'Unknown'}</Pill>;
}
export function MaintenanceStatusPill({ status }: { status: number }) {
  const tone: Tone = status === 3 ? 'ok' : status === 4 ? 'mut' : status === 2 ? 'info' : 'warn';
  return <Pill tone={tone}>{MaintenanceStatusLabel[status] ?? 'Unknown'}</Pill>;
}
export function ServiceStatusPill({ status }: { status: string }) {
  const map: Record<string, Tone> = {
    Pending: 'warn',
    Accepted: 'info',
    InProgress: 'info',
    Completed: 'ok',
    Cancelled: 'mut',
    Confirmed: 'ok',
  };
  return <Pill tone={map[status] ?? 'mut'}>{status}</Pill>;
}

/* ---------- Avatar ---------- */
export function Avatar({ name, src, size = 40 }: { name: string; src?: string | null; size?: number }) {
  if (src) {
    return (
      <img
        src={src}
        alt={name}
        style={{ width: size, height: size }}
        className="rounded-full object-cover ring-1 ring-line"
      />
    );
  }
  return (
    <span
      style={{ width: size, height: size, background: avatarColor(name), fontSize: size * 0.4 }}
      className="grid place-items-center rounded-full font-bold text-white"
      aria-hidden
    >
      {initials(name)}
    </span>
  );
}
