import { Link } from 'react-router-dom';
import { useT } from '../../lib/i18n';
import {
  FacebookIcon,
  HexIcon,
  InstagramIcon,
  LinkedinIcon,
  MapPinIcon,
  ShieldIcon,
  TwitterIcon,
} from './icons';

// Column headings run through i18n at render time.
const COLUMNS: { heading: string; links: { label: string; to: string }[] }[] = [
  {
    heading: 'Explore',
    links: [
      { label: 'Search homes', to: '/search' },
      { label: 'Nearby places', to: '/nearby' },
      { label: 'Saved listings', to: '/saved' },
      { label: 'Why TripNest', to: '/explore' },
    ],
  },
  {
    heading: 'Hosting',
    links: [
      { label: 'Become a landlord', to: '/welcome?mode=signup&role=landlord' },
      { label: 'Host dashboard', to: '/landlord' },
      { label: 'Caretakers', to: '/caretakers' },
      { label: 'Agents', to: '/agents' },
    ],
  },
  {
    heading: 'Support',
    links: [
      { label: 'Help & support', to: '/help' },
      { label: 'Maintenance', to: '/maintenance' },
      { label: 'Terms & conditions', to: '#' },
      { label: 'Privacy policy', to: '#' },
    ],
  },
];

const SOCIALS = [
  { label: 'TripNest on Facebook', icon: <FacebookIcon size={16} /> },
  { label: 'TripNest on Instagram', icon: <InstagramIcon size={16} /> },
  { label: 'TripNest on Twitter', icon: <TwitterIcon size={16} /> },
  { label: 'TripNest on LinkedIn', icon: <LinkedinIcon size={16} /> },
];

const PROVIDERS = [
  { name: 'MTN Mobile Money', src: '/payments/mtn.svg' },
  { name: 'Telecel Cash', src: '/payments/telecel.png' },
  { name: 'AirtelTigo Money', src: '/payments/airteltigo.png' },
  { name: 'Visa', src: '/payments/visa.svg' },
  { name: 'Mastercard', src: '/payments/mastercard.svg' },
];

function FooterLink({ label, to }: { label: string; to: string }) {
  const cls = 'text-sm text-black no-underline transition-colors hover:text-black';
  return to.startsWith('#') ? (
    <a href={to} className={cls}>{label}</a>
  ) : (
    <Link to={to} className={cls}>{label}</Link>
  );
}

export default function Footer() {
  const t = useT();
  return (
    <footer className="bg-white text-black">
      {/* Brand accent line */}
      <div aria-hidden className="h-1 bg-gradient-to-r from-brand via-emerald-400 to-brand" />

      <div className="mx-auto max-w-6xl px-6">
        {/* Top: brand + link columns */}
        <div className="grid grid-cols-1 gap-10 py-12 md:grid-cols-12">
          <div className="md:col-span-5 lg:col-span-4">
            <div className="flex items-center gap-2.5">
              <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-brand to-emerald-500 text-black">
                <HexIcon size={20} />
              </span>
              <span>
                <span className="block text-lg font-bold leading-tight">TripNest</span>
                <span className="block text-xs uppercase tracking-widest text-emerald-300">
                  Find · Stay · Thrive
                </span>
              </span>
            </div>
            <p className="mt-4 max-w-xs text-sm leading-relaxed text-black">
              {t('Verified homes across Ghana with identity-checked hosts and escrow-protected payments — from one night to a whole year.')}
            </p>
            <div className="mt-5 flex items-center gap-2">
              {SOCIALS.map((s) => (
                <a
                  key={s.label}
                  href="#"
                  aria-label={s.label}
                  className="flex h-9 w-9 items-center justify-center rounded-full border border-black text-black transition-colors hover:border-emerald-400 hover:text-emerald-300"
                >
                  {s.icon}
                </a>
              ))}
            </div>
          </div>

          <div className="grid grid-cols-2 gap-8 sm:grid-cols-3 md:col-span-7 lg:col-span-8">
            {COLUMNS.map((col) => (
              <div key={col.heading}>
                <h3 className="mb-3 text-xs font-semibold uppercase tracking-widest text-black">
                  {t(col.heading)}
                </h3>
                <ul className="space-y-2.5">
                  {col.links.map((l) => (
                    <li key={l.label}>
                      <FooterLink to={l.to} label={t(l.label)} />
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        </div>

        {/* Trust strip */}
        <div className="flex flex-col gap-4 border-t border-black py-5 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex flex-wrap items-center gap-2">
            <span className="mr-1 text-sm text-black">{t('We accept')}</span>
            {PROVIDERS.map((p) => (
              <span
                key={p.name}
                title={p.name}
                className="flex h-9 items-center rounded-lg border border-gray-200 bg-white px-2.5"
              >
                <img
                  src={p.src}
                  alt={p.name}
                  loading="lazy"
                  className="h-6 w-auto object-contain"
                />
              </span>
            ))}
          </div>
          <div className="flex flex-wrap items-center gap-x-5 gap-y-2 text-sm text-black">
            <span className="flex items-center gap-1.5">
              <ShieldIcon size={14} className="text-emerald-300" /> {t('Escrow-protected payments')}
            </span>
            <span className="flex items-center gap-1.5">
              <MapPinIcon size={14} className="text-emerald-300" /> {t('Made in Ghana')}
            </span>
          </div>
        </div>

        {/* Bottom bar */}
        <div className="flex flex-col gap-2 border-t border-black py-5 text-sm text-black sm:flex-row sm:items-center sm:justify-between">
          <p>© {new Date().getFullYear()} TripNest. All rights reserved.</p>
          <p>
            Built for renters and hosts across{' '}
            <span className="font-semibold text-black">18+ Ghanaian cities</span>.
          </p>
        </div>
      </div>
    </footer>
  );
}
