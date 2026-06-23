import { Link } from 'react-router-dom';
import { Button } from '@/components/ui';
import { Shield, Cash, Star } from '@/components/icons';

const stats = [
  { value: '100%', label: 'Verified hosts & agents' },
  { value: 'Escrow', label: 'Protected payments' },
  { value: 'NIA', label: 'Ghana Card matched' },
];

export default function AboutPage() {
  return (
    <div>
      <section className="container-tn max-w-3xl py-16 text-center">
        <h1 className="text-3xl font-extrabold sm:text-4xl">Renting in Ghana, without the fear</h1>
        <p className="mx-auto mt-4 max-w-xl text-muted">
          Too many people lose money to fake listings and agents who vanish. TripNest exists to make every booking
          safe: real identities, real properties, and payments that only move when you’re protected.
        </p>
      </section>

      <section className="border-y border-line bg-surface py-10">
        <div className="container-tn grid max-w-4xl grid-cols-3 gap-6 text-center">
          {stats.map((s) => (
            <div key={s.label}>
              <p className="text-2xl font-extrabold text-brand-600 sm:text-3xl">{s.value}</p>
              <p className="mt-1 text-xs font-semibold text-muted sm:text-sm">{s.label}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="container-tn max-w-4xl py-14">
        <div className="grid grid-cols-1 gap-8 sm:grid-cols-3">
          <div>
            <Shield className="h-7 w-7 text-brand-600" />
            <h3 className="mt-3 font-bold">Verify everyone</h3>
            <p className="mt-1 text-sm text-muted">Identity checks against the national registry before anyone can list or transact.</p>
          </div>
          <div>
            <Cash className="h-7 w-7 text-brand-600" />
            <h3 className="mt-3 font-bold">Protect every cedi</h3>
            <p className="mt-1 text-sm text-muted">Escrow holds payment until check-in is confirmed — disputes are reviewed fairly.</p>
          </div>
          <div>
            <Star className="h-7 w-7 text-brand-600" />
            <h3 className="mt-3 font-bold">Earn trust openly</h3>
            <p className="mt-1 text-sm text-muted">Reality scores and honest reviews keep the whole marketplace accountable.</p>
          </div>
        </div>

        <div className="mt-12 text-center">
          <Link to="/signup">
            <Button>Join TripNest</Button>
          </Link>
        </div>
      </section>
    </div>
  );
}
