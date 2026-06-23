import { Link } from 'react-router-dom';
import { Logo } from './Logo';

const cols: { title: string; links: { label: string; to: string }[] }[] = [
  {
    title: 'Discover',
    links: [
      { label: 'Search homes', to: '/search' },
      { label: 'Caretakers', to: '/services?tab=caretakers' },
      { label: 'Agents', to: '/services?tab=agents' },
      { label: 'Become a host', to: '/signup?role=landlord' },
    ],
  },
  {
    title: 'Trust & safety',
    links: [
      { label: 'Ghana Card verification', to: '/verification' },
      { label: 'Escrow protection', to: '/about#escrow' },
      { label: 'Safe-arrival check-in', to: '/about#safety' },
      { label: 'Reality score', to: '/about#trust' },
    ],
  },
  {
    title: 'Company',
    links: [
      { label: 'About us', to: '/about' },
      { label: 'How it works', to: '/about#how' },
      { label: 'Terms & conditions', to: '/about#terms' },
      { label: 'Privacy policy', to: '/about#privacy' },
    ],
  },
];

export function Footer() {
  return (
    <footer className="mt-16 border-t border-line bg-white">
      <div className="container-tn grid gap-8 py-12 md:grid-cols-4">
        <div>
          <Logo />
          <p className="mt-3 max-w-xs text-sm text-muted">
            Trusted, escrow-protected rentals across Ghana — every host verified by Ghana Card.
          </p>
          <div className="mt-4 flex items-center gap-2 text-xs font-semibold text-muted">
            <span>We accept</span>
            <span className="rounded bg-gold-500/15 px-2 py-1 text-gold-700">MTN MoMo</span>
            <span className="rounded bg-brand-50 px-2 py-1 text-brand-700">Cards</span>
          </div>
        </div>
        {cols.map((c) => (
          <div key={c.title}>
            <h4 className="mb-3 text-sm font-bold text-ink">{c.title}</h4>
            <ul className="space-y-2">
              {c.links.map((l) => (
                <li key={l.label}>
                  <Link to={l.to} className="text-sm text-muted hover:text-brand-700">
                    {l.label}
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
      <div className="border-t border-line">
        <div className="container-tn flex flex-col items-center justify-between gap-2 py-5 text-xs text-muted sm:flex-row">
          <p>© {new Date().getFullYear()} TripNest. All rights reserved.</p>
          <p>Made in Ghana 🇬🇭 · Find · Stay · Thrive</p>
        </div>
      </div>
    </footer>
  );
}
