import { Link } from 'react-router-dom';

export function Logo({ to = '/', light = false }: { to?: string; light?: boolean }) {
  return (
    <Link to={to} className="flex items-center gap-2 font-extrabold">
      <span className="grid h-9 w-9 place-items-center rounded-xl bg-brand-600 text-white shadow-glow">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M3 10.5 12 3l9 7.5" />
          <path d="M5 9.5V21h14V9.5" />
          <path d="m9.5 13.5 1.7 1.7 3.3-3.4" />
        </svg>
      </span>
      <span className="leading-none">
        <span className={`block text-lg ${light ? 'text-white' : 'text-ink'}`}>
          Trip<span className="text-brand-600">Nest</span>
        </span>
        <span className={`block text-[10px] font-semibold tracking-wide ${light ? 'text-white/70' : 'text-muted'}`}>
          Find · Stay · Thrive
        </span>
      </span>
    </Link>
  );
}
