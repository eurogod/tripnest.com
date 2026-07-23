import { useEffect, useMemo, useState } from 'react';
import CalendarSyncSection from '../components/landlord/CalendarSyncSection';
import type { CalendarMonth, Listing } from '../types';
import { getCalendarMonth } from '../api/calendar';
import { getListings } from '../api/listings';
import { useAsync } from '../hooks/useAsync';
import AsyncBoundary from '../components/AsyncBoundary';
import Button from '../components/ui/Button';

const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

const OWNER_HATCH =
  'bg-[repeating-linear-gradient(45deg,#bbf7d0_0,#bbf7d0_5px,transparent_5px,transparent_10px)]';
const MAINT_HATCH =
  'bg-[repeating-linear-gradient(45deg,#fecaca_0,#fecaca_5px,transparent_5px,transparent_10px)]';

const Moon = () => (
  <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false">
    <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />
  </svg>
);

const Globe = () => (
  <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true" focusable="false">
    <circle cx="12" cy="12" r="10" />
    <line x1="2" y1="12" x2="22" y2="12" />
    <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z" />
  </svg>
);

/** Split the month into weeks of 7 cells, padding leading/trailing blanks. */
function buildWeeks(daysInMonth: number, firstWeekday: number): (number | null)[][] {
  const cells: (number | null)[] = [];
  for (let i = 0; i < firstWeekday; i++) cells.push(null);
  for (let d = 1; d <= daysInMonth; d++) cells.push(d);
  while (cells.length % 7 !== 0) cells.push(null);

  const weeks: (number | null)[][] = [];
  for (let i = 0; i < cells.length; i += 7) weeks.push(cells.slice(i, i + 7));
  return weeks;
}

interface Segment {
  startCol: number;
  endCol: number;
  label: string;
  showLabel: boolean;
  lane: number;
}

/** Booking bars overlapping a given week, greedily packed into lanes. */
function weekSegments(week: (number | null)[], month: CalendarMonth): Segment[] {
  const raw = month.bookings
    .map((b) => {
      const cols = week
        .map((day, col) => ({ day, col }))
        .filter((c) => c.day != null && c.day >= b.startDate && c.day <= b.endDate);
      if (cols.length === 0) return null;
      return {
        startCol: cols[0].col,
        endCol: cols[cols.length - 1].col,
        label: b.label,
        showLabel: cols[0].day === b.startDate,
      };
    })
    .filter((s): s is Omit<Segment, 'lane'> => s !== null)
    .sort((a, b) => a.startCol - b.startCol);

  const laneEnds: number[] = [];
  return raw.map((s) => {
    let lane = laneEnds.findIndex((end) => end < s.startCol);
    if (lane === -1) lane = laneEnds.length;
    laneEnds[lane] = s.endCol;
    return { ...s, lane };
  });
}

function priceColor(day: number, month: CalendarMonth): string {
  if (month.discountDays.includes(day)) return 'text-blue-600';
  if (month.ownerDays.includes(day)) return 'text-amber-600';
  return 'text-ink';
}

function LegendDot({ className }: { className: string }) {
  return <span className={`h-3.5 w-3.5 rounded-sm border border-gray-200 ${className}`} />;
}

function CalendarGrid({ month }: { month: CalendarMonth }) {
  const weeks = useMemo(() => {
    const [monStr, yearStr] = month.label.split(' ');
    const monthIndex = MONTHS.indexOf(monStr);
    const year = Number(yearStr);
    const firstWeekday = new Date(year, monthIndex, 1).getDay();
    const daysInMonth = new Date(year, monthIndex + 1, 0).getDate();
    return buildWeeks(daysInMonth, firstWeekday);
  }, [month.label]);

  return (
    <div className="min-w-190 overflow-hidden rounded-xl border border-gray-200">
      <div className="grid grid-cols-7 border-b border-gray-200 bg-gray-50">
        {WEEKDAYS.map((d) => (
          <div key={d} className="px-3 py-2.5 text-sm font-medium text-muted">
            {d}
          </div>
        ))}
      </div>

      {weeks.map((week, wi) => {
        const segments = weekSegments(week, month);
        return (
          <div key={wi} className="relative grid grid-cols-7">
            {week.map((day, col) => (
              <div
                key={col}
                className="relative min-h-[116px] border-b border-l border-gray-200 first:border-l-0 last:border-r-0"
              >
                {day != null && (
                  <>
                    <div className="flex items-start justify-between px-3 pt-2">
                      <span className={`font-semibold ${priceColor(day, month)}`}>
                        ₵{month.prices[day]}
                      </span>
                      <span className="text-sm text-muted">{day}</span>
                    </div>
                    <div className="mt-1 flex items-center gap-1 px-3 text-muted">
                      <Moon />
                      <span className="text-xs">{month.minNights}</span>
                    </div>
                    {month.ownerDays.includes(day) && (
                      <div className={`absolute inset-x-0 bottom-0 h-3 ${OWNER_HATCH}`} />
                    )}
                    {month.maintenanceDays.includes(day) && (
                      <div className={`absolute inset-x-0 bottom-0 h-3 ${MAINT_HATCH}`} />
                    )}
                  </>
                )}
              </div>
            ))}

            {segments.map((seg, si) => (
              <div
                key={si}
                className="pointer-events-none absolute flex h-6 items-center gap-1.5 rounded-md bg-brand-50 px-2 text-xs font-medium text-brand"
                style={{
                  left: `calc(${(seg.startCol / 7) * 100}% + 4px)`,
                  width: `calc(${((seg.endCol - seg.startCol + 1) / 7) * 100}% - 8px)`,
                  bottom: `${20 + seg.lane * 28}px`,
                }}
              >
                {seg.showLabel && (
                  <>
                    <Globe />
                    <span className="truncate">{seg.label}</span>
                  </>
                )}
              </div>
            ))}
          </div>
        );
      })}
    </div>
  );
}

function CalendarView({ listings }: { listings: Listing[] }) {
  const [propertyId, setPropertyId] = useState(listings[0].id);
  const now = new Date();
  const [cursor, setCursor] = useState({ year: now.getFullYear(), month: now.getMonth() });
  const listing = listings.find((l) => l.id === propertyId) ?? listings[0];

  const state = useAsync(
    () => getCalendarMonth(listing, cursor.year, cursor.month),
    [listing.id, cursor.year, cursor.month],
  );

  const shiftMonth = (delta: number) =>
    setCursor(({ year, month }) => {
      const d = new Date(year, month + delta, 1);
      return { year: d.getFullYear(), month: d.getMonth() };
    });

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <select
            value={listing.id}
            onChange={(e) => setPropertyId(e.target.value)}
            className="rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm font-medium text-ink outline-none focus:border-brand"
          >
            {listings.map((l) => (
              <option key={l.id} value={l.id}>
                {l.title}
              </option>
            ))}
          </select>
          <Button variant="ghost" size="sm" onClick={() => shiftMonth(-1)} aria-label="Previous month">
            ←
          </Button>
          <h2 className="w-28 text-center text-xl font-bold text-ink">{MONTHS[cursor.month]} {cursor.year}</h2>
          <Button variant="ghost" size="sm" onClick={() => shiftMonth(1)} aria-label="Next month">
            →
          </Button>
        </div>
        <div className="flex flex-wrap items-center gap-5 text-sm text-muted">
          <span className="flex items-center gap-1.5">
            <Moon /> Minimum nights
          </span>
          <span className="flex items-center gap-1.5">
            <LegendDot className={OWNER_HATCH} /> Blocked / owner use
          </span>
          <span className="flex items-center gap-1.5">
            <LegendDot className={MAINT_HATCH} /> Maintenance
          </span>
        </div>
      </div>
      <AsyncBoundary
        state={state}
        loadingMessage="Loading calendar…"
        errorMessage="Failed to load calendar."
      >
        {(data) => (
          <div className="overflow-x-auto">
            <CalendarGrid month={data} />
          </div>
        )}
      </AsyncBoundary>
    </>
  );
}

export default function CalendarPage() {
  const listingsState = useAsync(getListings, []);
  const [blocking, setBlocking] = useState(false);

  useEffect(() => {
    if (!blocking) return;
    const t = setTimeout(() => setBlocking(false), 4000);
    return () => clearTimeout(t);
  }, [blocking]);

  return (
    <div>
      <div className="mb-8 flex items-center justify-between">
        <h1 className="text-4xl font-bold text-ink">Calendar</h1>
        <Button variant="dark" onClick={() => setBlocking(true)}>Book dates for yourself</Button>
      </div>

      {blocking && (
        <div className="mb-6 rounded-xl border border-amber-100 bg-amber-50 px-4 py-3 text-sm font-medium text-amber-700">
          Owner-booking mode: pick the days on the calendar below to block them for personal use.
        </div>
      )}

      <AsyncBoundary
        state={listingsState}
        loadingMessage="Loading your listings…"
        errorMessage="Failed to load your listings."
      >
        {(listings) =>
          listings.length > 0 ? (
            <CalendarView listings={listings} />
          ) : (
            <p className="text-muted">Publish a listing to manage its calendar.</p>
          )
        }
      </AsyncBoundary>

      <CalendarSyncSection />
    </div>
  );
}
