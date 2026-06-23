import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { propertiesApi, agentsApi, caretakersApi } from '@/lib/services';
import { PropertyCard, PropertyCardSkeleton } from '@/components/PropertyCard';
import { Button } from '@/components/ui';
import { Avatar, StarRating, VerifiedBadge } from '@/components/badges';
import { Calendar, MapPin, Search as SearchIcon, Shield, Users, Wrench, Check, Cash, Camera } from '@/components/icons';
import { TOWNS } from '@/lib/hooks';

const CATEGORIES = ['All', 'Apartments', 'Houses', 'Single Room', 'Short stay', 'Long-term', 'Student', 'Verified hosts'];

export default function LandingPage() {
  const navigate = useNavigate();
  const [location, setLocation] = useState('');
  const [guests, setGuests] = useState(1);

  const { data: properties, isLoading } = useQuery({ queryKey: ['properties'], queryFn: propertiesApi.list });
  const { data: agents } = useQuery({ queryKey: ['agents'], queryFn: agentsApi.list });
  const { data: caretakers } = useQuery({ queryKey: ['caretakers'], queryFn: caretakersApi.list });

  const featured = (properties ?? []).slice(0, 8);

  const doSearch = (e: React.FormEvent) => {
    e.preventDefault();
    const params = new URLSearchParams();
    if (location.trim()) params.set('location', location.trim());
    if (guests > 1) params.set('guests', String(guests));
    navigate(`/search?${params.toString()}`);
  };

  return (
    <div>
      {/* ---------- Hero ---------- */}
      <section className="relative overflow-hidden">
        <div className="absolute inset-0 -z-10">
          <img
            src="https://images.unsplash.com/photo-1564013799919-ab600027ffc6?auto=format&fit=crop&w=1600&q=70"
            alt=""
            className="h-full w-full object-cover"
          />
          <div className="absolute inset-0 bg-gradient-to-r from-brand-900/90 via-brand-800/70 to-brand-700/30" />
        </div>
        <div className="container-tn py-16 sm:py-24">
          <div className="max-w-2xl text-white">
            <span className="pill bg-white/15 text-white ring-1 ring-white/25">
              <Shield className="h-3.5 w-3.5" /> Ghana Card-verified hosts
            </span>
            <h1 className="mt-4 text-4xl font-extrabold leading-tight sm:text-5xl">
              Find <span className="text-gold-400">verified</span> accommodation across Ghana
            </h1>
            <p className="mt-3 max-w-lg text-base text-white/85">
              Trusted rentals with escrow-protected payments, SMS safety alerts and digital agreements.
              Every home, every host — verified.
            </p>
          </div>

          {/* Search widget */}
          <form
            onSubmit={doSearch}
            className="mt-8 flex max-w-3xl flex-col gap-2 rounded-2xl bg-white p-2 shadow-lg sm:flex-row sm:items-center"
          >
            <label className="flex flex-1 items-center gap-2 rounded-xl px-3 py-2 hover:bg-surface">
              <MapPin className="h-5 w-5 text-brand-600" />
              <span className="flex flex-col">
                <span className="text-[11px] font-bold uppercase tracking-wide text-muted">Location</span>
                <input
                  list="towns"
                  value={location}
                  onChange={(e) => setLocation(e.target.value)}
                  placeholder="Where in Ghana?"
                  className="w-full bg-transparent text-sm font-semibold outline-none placeholder:font-normal placeholder:text-muted"
                />
                <datalist id="towns">
                  {Object.keys(TOWNS).map((t) => (
                    <option key={t} value={t} />
                  ))}
                </datalist>
              </span>
            </label>
            <span className="hidden h-9 w-px bg-line sm:block" />
            <label className="flex flex-1 items-center gap-2 rounded-xl px-3 py-2 hover:bg-surface">
              <Calendar className="h-5 w-5 text-brand-600" />
              <span className="flex flex-col">
                <span className="text-[11px] font-bold uppercase tracking-wide text-muted">Move-in</span>
                <input type="date" className="w-full bg-transparent text-sm font-semibold outline-none" />
              </span>
            </label>
            <span className="hidden h-9 w-px bg-line sm:block" />
            <label className="flex items-center gap-2 rounded-xl px-3 py-2 hover:bg-surface">
              <Users className="h-5 w-5 text-brand-600" />
              <span className="flex flex-col">
                <span className="text-[11px] font-bold uppercase tracking-wide text-muted">Guests</span>
                <select
                  value={guests}
                  onChange={(e) => setGuests(Number(e.target.value))}
                  className="bg-transparent text-sm font-semibold outline-none"
                >
                  {[1, 2, 3, 4, 5, 6].map((n) => (
                    <option key={n} value={n}>
                      {n} guest{n > 1 ? 's' : ''}
                    </option>
                  ))}
                </select>
              </span>
            </label>
            <Button type="submit" className="h-12 px-6">
              <SearchIcon className="h-4 w-4" /> Search
            </Button>
          </form>

          <div className="mt-5 flex flex-wrap gap-3 text-sm font-semibold text-white/90">
            {['Verified Listings', 'Mobile Money Payments', 'SMS Safety Alerts', 'Digital Agreements', '24/7 Support'].map(
              (f) => (
                <span key={f} className="inline-flex items-center gap-1.5">
                  <Check className="h-4 w-4 text-gold-400" /> {f}
                </span>
              ),
            )}
          </div>
        </div>
      </section>

      {/* ---------- Category strip ---------- */}
      <div className="border-b border-line bg-white">
        <div className="container-tn flex gap-2 overflow-x-auto py-3">
          {CATEGORIES.map((c, i) => (
            <button
              key={c}
              onClick={() => navigate(`/search?category=${encodeURIComponent(c)}`)}
              className={`whitespace-nowrap rounded-full border px-4 py-1.5 text-sm font-semibold transition ${
                i === 0 ? 'border-brand-600 bg-brand-600 text-white' : 'border-line text-ink hover:border-ink'
              }`}
            >
              {c}
            </button>
          ))}
        </div>
      </div>

      {/* ---------- Featured properties ---------- */}
      <section className="container-tn py-12">
        <div className="mb-6 flex items-end justify-between">
          <div>
            <h2 className="text-2xl font-extrabold">Featured verified homes</h2>
            <p className="text-sm text-muted">Hand-picked stays from Ghana Card-verified hosts.</p>
          </div>
          <Link to="/search" className="text-sm font-bold text-brand-700 hover:underline">
            View all →
          </Link>
        </div>
        <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-4">
          {isLoading
            ? Array.from({ length: 8 }).map((_, i) => <PropertyCardSkeleton key={i} />)
            : featured.length
              ? featured.map((p) => <PropertyCard key={p.propertyId} p={p} />)
              : (
                <p className="col-span-full rounded-xl border border-dashed border-line py-10 text-center text-muted">
                  No live listings yet — check back soon, or list your property.
                </p>
              )}
        </div>
      </section>

      {/* ---------- Why TripNest ---------- */}
      <section className="border-y border-line bg-white py-14">
        <div className="container-tn">
          <h2 className="text-center text-2xl font-extrabold">Why choose TripNest?</h2>
          <div className="mt-8 grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
            {[
              { icon: <Shield className="h-6 w-6" />, t: 'TripNest ID', d: 'Every host and listing verified by Ghana Card.' },
              { icon: <Cash className="h-6 w-6" />, t: 'Escrow payments', d: 'Funds held safely and released only after you move in.' },
              { icon: <Users className="h-6 w-6" />, t: 'Verified agents', d: 'Licensed agents you can rely on for viewings.' },
              { icon: <Wrench className="h-6 w-6" />, t: 'Caretaker support', d: 'On-site help whenever you need it.' },
            ].map((f) => (
              <div key={f.t} className="card p-5">
                <div className="grid h-11 w-11 place-items-center rounded-xl bg-brand-50 text-brand-600">{f.icon}</div>
                <h3 className="mt-3 font-bold">{f.t}</h3>
                <p className="mt-1 text-sm text-muted">{f.d}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ---------- Caretakers & Agents showcase ---------- */}
      <ServiceShowcase
        title="Trusted caretakers"
        subtitle="On-site help for cleaning, repairs and property care."
        icon={<Wrench className="h-5 w-5" />}
        viewAll="/services?tab=caretakers"
        items={(caretakers ?? []).slice(0, 4).map((c) => ({
          id: c.caretakerId,
          name: `Caretaker ${c.caretakerId.slice(0, 4).toUpperCase()}`,
          meta: c.responsibilities || 'General property care',
        }))}
      />
      <ServiceShowcase
        title="Licensed agents"
        subtitle="Book a viewing with a verified TripNest agent near you."
        icon={<Users className="h-5 w-5" />}
        viewAll="/services?tab=agents"
        muted
        items={(agents ?? []).slice(0, 4).map((a) => ({
          id: a.agentId,
          name: `Agent ${a.licenseNumber}`,
          meta: a.bio || `${a.yearsOfExperience ?? 0} yrs experience`,
        }))}
      />

      {/* ---------- How it works ---------- */}
      <section className="container-tn py-14">
        <h2 className="text-center text-2xl font-extrabold">How it works</h2>
        <div className="mt-8 grid gap-6 sm:grid-cols-4">
          {[
            { icon: <SearchIcon className="h-6 w-6" />, t: 'Search', d: 'Find the perfect verified home.' },
            { icon: <Users className="h-6 w-6" />, t: 'Connect', d: 'Chat with the host or agent.' },
            { icon: <Cash className="h-6 w-6" />, t: 'Book securely', d: 'Pay into escrow, sign your agreement.' },
            { icon: <Camera className="h-6 w-6" />, t: 'Move in', d: 'Check in safely and enjoy.' },
          ].map((s, i) => (
            <div key={s.t} className="relative text-center">
              <div className="mx-auto grid h-14 w-14 place-items-center rounded-full bg-brand-600 text-white shadow-glow">
                {s.icon}
              </div>
              <span className="absolute left-1/2 top-0 -ml-12 text-3xl font-extrabold text-line">{i + 1}</span>
              <h3 className="mt-3 font-bold">{s.t}</h3>
              <p className="mt-1 text-sm text-muted">{s.d}</p>
            </div>
          ))}
        </div>
      </section>

      {/* ---------- Testimonials ---------- */}
      <section className="border-t border-line bg-white py-14">
        <div className="container-tn">
          <h2 className="text-center text-2xl font-extrabold">What people say</h2>
          <div className="mt-8 grid gap-5 md:grid-cols-3">
            {[
              { n: 'Ama Serwaa', r: 'Student, UMaT', q: 'TripNest made it so easy to find a safe, affordable room near campus. The SMS safety alerts give me peace of mind.' },
              { n: 'Kofi Mensah', r: 'Tenant, Accra', q: 'Escrow meant I never had to worry about losing money to a fake landlord. Everything was verified.' },
              { n: 'Yaa Asantewaa', r: 'Landlord, Kumasi', q: 'I list with confidence — verified tenants and digital agreements save me so much stress.' },
            ].map((t) => (
              <figure key={t.n} className="card p-5">
                <StarRating value={5} />
                <blockquote className="mt-3 text-sm text-ink">“{t.q}”</blockquote>
                <figcaption className="mt-4 flex items-center gap-3">
                  <Avatar name={t.n} />
                  <span>
                    <span className="block text-sm font-bold">{t.n}</span>
                    <span className="block text-xs text-muted">{t.r}</span>
                  </span>
                </figcaption>
              </figure>
            ))}
          </div>
        </div>
      </section>

      {/* ---------- Become a host CTA ---------- */}
      <section className="container-tn py-14">
        <div className="overflow-hidden rounded-2xl bg-brand-700 p-8 text-white sm:p-12">
          <div className="max-w-xl">
            <h2 className="text-2xl font-extrabold sm:text-3xl">Become a host. Start earning today.</h2>
            <p className="mt-2 text-white/85">
              List your property to thousands of verified tenants. Get paid securely through escrow, with digital
              agreements and TripNest support every step of the way.
            </p>
            <Button variant="gold" className="mt-5" onClick={() => navigate('/signup?role=landlord')}>
              Get started
            </Button>
          </div>
        </div>
      </section>
    </div>
  );
}

function ServiceShowcase({
  title,
  subtitle,
  icon,
  items,
  viewAll,
  muted,
}: {
  title: string;
  subtitle: string;
  icon: React.ReactNode;
  items: { id: string; name: string; meta: string }[];
  viewAll: string;
  muted?: boolean;
}) {
  return (
    <section className={muted ? 'bg-surface py-12' : 'py-12'}>
      <div className="container-tn">
        <div className="mb-6 flex items-end justify-between">
          <div>
            <h2 className="flex items-center gap-2 text-2xl font-extrabold">
              <span className="grid h-9 w-9 place-items-center rounded-lg bg-brand-50 text-brand-600">{icon}</span>
              {title}
            </h2>
            <p className="text-sm text-muted">{subtitle}</p>
          </div>
          <Link to={viewAll} className="text-sm font-bold text-brand-700 hover:underline">
            View all →
          </Link>
        </div>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {items.length ? (
            items.map((it) => (
              <Link key={it.id} to={viewAll} className="card flex items-center gap-3 p-4 transition hover:shadow-soft">
                <Avatar name={it.name} size={48} />
                <span className="min-w-0">
                  <span className="flex items-center gap-1.5 font-bold">
                    {it.name} <VerifiedBadge size="sm" label="" />
                  </span>
                  <span className="line-clamp-1 text-sm text-muted">{it.meta}</span>
                </span>
              </Link>
            ))
          ) : (
            <p className="col-span-full rounded-xl border border-dashed border-line py-8 text-center text-sm text-muted">
              No {title.toLowerCase()} listed yet.
            </p>
          )}
        </div>
      </div>
    </section>
  );
}
