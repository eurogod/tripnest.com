import Card from '../../ui/Card';
import Avatar from '../../ui/Avatar';
import {
  BadgeIcon, BellIcon, UserCheckIcon, UsersIcon, FileIcon, CardIcon,
  SearchIcon, MessageIcon, CalendarIcon, KeyIcon, StarIcon,
} from '../icons';

const WHY = [
  { title: 'TripNest ID', desc: 'Unique ID for every property & user', icon: <BadgeIcon size={20} /> },
  { title: 'SMS Safety Alerts', desc: 'Instant alerts for your safety', icon: <BellIcon size={20} /> },
  { title: 'Verified Agents', desc: 'Trusted agents you can rely on', icon: <UserCheckIcon size={20} /> },
  { title: 'Caretaker Support', desc: 'On-site help when you need it', icon: <UsersIcon size={20} /> },
  { title: 'Digital Agreements', desc: 'Legally valid rental agreements', icon: <FileIcon size={20} /> },
  { title: 'Secure Payments', desc: 'Pay safely via Mobile Money', icon: <CardIcon size={20} /> },
];

const STEPS = [
  { title: 'Search', desc: 'Find the perfect property', icon: <SearchIcon size={20} /> },
  { title: 'Connect', desc: 'Contact agent or landlord', icon: <MessageIcon size={20} /> },
  { title: 'Book', desc: 'Reserve & make secure payment', icon: <CalendarIcon size={20} /> },
  { title: 'Move in', desc: 'Enjoy your new home', icon: <KeyIcon size={20} /> },
];

export default function InfoSections() {
  return (
    <div className="space-y-8">
      <section>
        <h2 className="mb-4 text-xl font-bold text-ink">Why choose TripNest?</h2>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {WHY.map((f) => (
            <Card key={f.title} className="flex items-start gap-3 p-4">
              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-brand-50 text-brand">
                {f.icon}
              </span>
              <span>
                <span className="block font-semibold text-ink">{f.title}</span>
                <span className="block text-sm text-muted">{f.desc}</span>
              </span>
            </Card>
          ))}
        </div>
      </section>

      <section>
        <h2 className="mb-4 text-xl font-bold text-ink">How it works</h2>
        <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
          {STEPS.map((s, i) => (
            <Card key={s.title} className="p-4 text-center">
              <span className="mx-auto flex h-11 w-11 items-center justify-center rounded-full bg-brand-50 text-brand">
                {s.icon}
              </span>
              <p className="mt-3 font-semibold text-ink">
                {i + 1}. {s.title}
              </p>
              <p className="text-sm text-muted">{s.desc}</p>
            </Card>
          ))}
        </div>
      </section>

      <section>
        <h2 className="mb-4 text-xl font-bold text-ink">What people say</h2>
        <Card className="p-6">
          <div className="mb-2 flex text-amber-400">
            {Array.from({ length: 5 }).map((_, i) => (
              <StarIcon key={i} size={16} />
            ))}
          </div>
          <p className="text-ink">
            “TripNest made it so easy to find a safe and affordable room near campus! The SMS
            safety alerts give me peace of mind.”
          </p>
          <div className="mt-4 flex items-center gap-3">
            <Avatar name="Ama Serwaa" size={40} />
            <span>
              <span className="block font-semibold text-ink">Ama Serwaa</span>
              <span className="block text-sm text-muted">Student, UMaT</span>
            </span>
          </div>
        </Card>
      </section>
    </div>
  );
}
