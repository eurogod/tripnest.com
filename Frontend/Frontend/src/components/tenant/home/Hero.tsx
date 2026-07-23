import { useNavigate } from 'react-router-dom';
import Button from '../../ui/Button';
import {
  MapPinIcon, CalendarIcon, UserIcon, SearchIcon,
  ShieldIcon, CardIcon, BellIcon, FileIcon, ClockIcon,
} from '../icons';

const TRUST = [
  { label: 'Verified Listings', icon: <ShieldIcon size={15} /> },
  { label: 'Mobile Money Payments', icon: <CardIcon size={15} /> },
  { label: 'SMS Safety Alerts', icon: <BellIcon size={15} /> },
  { label: 'Digital Agreements', icon: <FileIcon size={15} /> },
  { label: '24/7 Support', icon: <ClockIcon size={15} /> },
];

function SearchField({
  label, icon, value,
}: { label: string; icon: React.ReactNode; value: string }) {
  return (
    <div className="flex flex-1 items-center gap-2 px-3 py-2">
      <span className="text-brand">{icon}</span>
      <span className="min-w-0">
        <span className="block text-[11px] text-muted">{label}</span>
        <span className="block truncate text-sm font-medium text-ink">{value}</span>
      </span>
    </div>
  );
}

export default function Hero() {
  const navigate = useNavigate();
  return (
    <section className="overflow-hidden rounded-2xl bg-gradient-to-br from-emerald-900 via-emerald-800 to-emerald-600 p-6 text-white sm:p-10">
      <h1 className="max-w-lg text-3xl font-bold sm:text-4xl">
        Find verified accommodation in <span className="text-emerald-200">Tarkwa</span>
      </h1>
      <p className="mt-3 max-w-md text-sm text-emerald-100">
        Trusted rentals with secure payments, SMS safety alerts and digital agreements.
      </p>

      <div className="mt-6 flex flex-col gap-2 rounded-2xl bg-white p-2 sm:flex-row sm:items-center">
        <SearchField label="Location" icon={<MapPinIcon size={18} />} value="Tarkwa, Ghana" />
        <span className="hidden h-8 w-px bg-gray-200 sm:block" />
        <SearchField label="Check in – Check out" icon={<CalendarIcon size={18} />} value="May 20 – May 27" />
        <span className="hidden h-8 w-px bg-gray-200 sm:block" />
        <SearchField label="Guests" icon={<UserIcon size={18} />} value="2 Guests" />
        <Button className="gap-2 sm:w-auto" onClick={() => navigate('/search')}>
          <SearchIcon size={16} /> Search
        </Button>
      </div>

      <div className="mt-5 flex flex-wrap gap-2">
        {TRUST.map((t) => (
          <span
            key={t.label}
            className="flex items-center gap-1.5 rounded-full bg-white/15 px-3 py-1.5 text-xs font-medium backdrop-blur"
          >
            {t.icon} {t.label}
          </span>
        ))}
      </div>
    </section>
  );
}
