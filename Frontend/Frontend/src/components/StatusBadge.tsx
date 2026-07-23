import type { ReservationStatus } from '../types';
import Badge, { type BadgeTone } from './ui/Badge';

const TONES: Record<ReservationStatus, BadgeTone> = {
  upcoming: 'green',
  complete: 'gray',
  canceled: 'red',
};

const LABELS: Record<ReservationStatus, string> = {
  upcoming: 'Upcoming',
  complete: 'Complete',
  canceled: 'Canceled',
};

export default function StatusBadge({ status }: { status: ReservationStatus }) {
  return <Badge tone={TONES[status]}>{LABELS[status]}</Badge>;
}
