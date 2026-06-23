import { Link } from 'react-router-dom';
import { Button } from '@/components/ui';
import { Shield, Users, Wrench, Cash, Camera, MapPin } from '@/components/icons';

const services = [
  {
    icon: <Shield className="h-6 w-6" />,
    title: 'Ghana Card verification',
    body: 'Every host, agent and caretaker is matched against the NIA registry with a live selfie before they can transact.',
  },
  {
    icon: <Cash className="h-6 w-6" />,
    title: 'Escrow-protected payments',
    body: 'Your money is held safely and only released to the host after a successful, verified check-in. Disputes are reviewed by our team.',
  },
  {
    icon: <Users className="h-6 w-6" />,
    title: 'Licensed agents',
    body: 'Book in-person viewings with verified agents who can show you the property and confirm it’s exactly as listed.',
  },
  {
    icon: <Wrench className="h-6 w-6" />,
    title: 'Caretakers & maintenance',
    body: 'Report an issue and we route it to a vetted caretaker. Track every request from reported to resolved.',
  },
  {
    icon: <Camera className="h-6 w-6" />,
    title: 'Walkthrough videos',
    body: 'Admin-reviewed walkthrough videos mean what you see is what you get — no surprises on arrival.',
  },
  {
    icon: <MapPin className="h-6 w-6" />,
    title: 'Real maps & locations',
    body: 'Pinned, accurate locations across Accra, Kumasi, Takoradi and beyond so you always know where you’re staying.',
  },
];

export default function ServicesPage() {
  return (
    <div>
      <section className="bg-gradient-to-br from-brand-700 to-brand-600 py-16 text-white">
        <div className="container-tn max-w-3xl text-center">
          <span className="pill bg-white/15 text-white ring-1 ring-white/25">Trust, built in</span>
          <h1 className="mt-4 text-3xl font-extrabold sm:text-4xl">Everything that makes a stay safe</h1>
          <p className="mx-auto mt-3 max-w-xl text-white/85">
            TripNest is a trust-first marketplace for Ghana. Verification, escrow and real people stand behind every booking.
          </p>
        </div>
      </section>

      <section className="container-tn max-w-6xl py-14">
        <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {services.map((s) => (
            <div key={s.title} className="card p-6">
              <div className="grid h-12 w-12 place-items-center rounded-xl bg-brand-50 text-brand-600">{s.icon}</div>
              <h3 className="mt-4 text-lg font-bold">{s.title}</h3>
              <p className="mt-1.5 text-sm text-muted">{s.body}</p>
            </div>
          ))}
        </div>

        <div className="mt-12 flex flex-col items-center gap-4 rounded-2xl bg-surface p-10 text-center">
          <h2 className="text-2xl font-extrabold">Ready to find your next home?</h2>
          <p className="max-w-md text-sm text-muted">Browse verified listings or create an account to start booking with confidence.</p>
          <div className="flex gap-3">
            <Link to="/search">
              <Button>Browse stays</Button>
            </Link>
            <Link to="/signup">
              <Button variant="outline">Create account</Button>
            </Link>
          </div>
        </div>
      </section>
    </div>
  );
}
