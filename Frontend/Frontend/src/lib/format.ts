const cedi = new Intl.NumberFormat('en-GH', {
  style: 'currency',
  currency: 'GHS',
});

/** Format a number as Ghana Cedi with pesewas, e.g. 1265.77 -> "GH₵1,265.77". */
export function formatCurrency(value: number): string {
  return cedi.format(value);
}

/** Format a number as Ghana Cedi, e.g. 1200 -> "GH₵ 1,200". */
export function formatCedi(value: number): string {
  return `GH₵ ${value.toLocaleString('en-GH')}`;
}

const shortDate = new Intl.DateTimeFormat('en-US', {
  month: 'short',
  day: 'numeric',
  year: 'numeric',
});

const fullDate = new Intl.DateTimeFormat('en-US', {
  weekday: 'short',
  month: 'short',
  day: 'numeric',
  year: 'numeric',
});

/** Parse an ISO date string (yyyy-mm-dd) as a local date, avoiding TZ drift. */
function parseISO(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, (m ?? 1) - 1, d ?? 1);
}

/** Format an ISO date, e.g. "2025-05-20" -> "May 20, 2025". */
export function formatDateShort(iso: string): string {
  return shortDate.format(parseISO(iso));
}

/** Format an ISO date, e.g. "2025-05-20" -> "Tue, May 20, 2025". */
export function formatDateFull(iso: string): string {
  return fullDate.format(parseISO(iso));
}

/** Whole nights between two ISO dates (checkOut - checkIn). */
export function nightsBetween(checkInISO: string, checkOutISO: string): number {
  const ms = parseISO(checkOutISO).getTime() - parseISO(checkInISO).getTime();
  return Math.max(0, Math.round(ms / 86_400_000));
}
