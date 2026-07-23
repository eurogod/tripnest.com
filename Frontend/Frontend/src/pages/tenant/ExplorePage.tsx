import { useEffect, useRef, useState } from 'react';
import { Link, Navigate } from 'react-router-dom';
import { useSession } from '../../store/authStore';
import Card from '../../components/ui/Card';
import Footer from '../../components/tenant/Footer';
import {
  CardIcon,
  ChatIcon,
  CheckIcon,
  FileIcon,
  HexIcon,
  KeyIcon,
  MapPinIcon,
  PlayIcon,
  SearchIcon,
  ShieldIcon,
  StarIcon,
  ToolIcon,
  UserCheckIcon,
  UserIcon,
} from '../../components/tenant/icons';

/** Fades children up once they scroll into view (CSS in index.css, reduced-motion aware). */
function Reveal({
  children,
  delay = 0,
  className = '',
}: {
  children: React.ReactNode;
  delay?: number;
  className?: string;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const [seen, setSeen] = useState(false);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const io = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          setSeen(true);
          io.disconnect();
        }
      },
      { threshold: 0.15, rootMargin: '0px 0px -40px 0px' },
    );
    io.observe(el);
    return () => io.disconnect();
  }, []);

  return (
    <div
      ref={ref}
      className={`tn-reveal ${seen ? 'tn-in' : ''} ${className}`}
      style={{ '--tn-delay': `${delay}ms` } as React.CSSProperties}
    >
      {children}
    </div>
  );
}

// --- Floating notch nav ------------------------------------------------------

const SECTIONS = [
  { id: 'features', label: 'Features' },
  { id: 'how-it-works', label: 'How it works' },
  { id: 'join', label: 'Join' },
] as const;

/**
 * Dynamic-island style pill that floats over the page: it condenses and gains
 * depth once you scroll, and a highlight slides between section links as the
 * page moves underneath it.
 */
function NotchNav() {
  const [scrolled, setScrolled] = useState(false);
  const [active, setActive] = useState<string>('');
  const linkRefs = useRef(new Map<string, HTMLButtonElement>());
  const [pill, setPill] = useState<{ left: number; width: number } | null>(null);

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 40);
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  // Track which section is under the viewport middle.
  useEffect(() => {
    const io = new IntersectionObserver(
      (entries) => {
        for (const e of entries) if (e.isIntersecting) setActive(e.target.id);
      },
      { rootMargin: '-40% 0px -50% 0px' },
    );
    SECTIONS.forEach((s) => {
      const el = document.getElementById(s.id);
      if (el) io.observe(el);
    });
    return () => io.disconnect();
  }, []);

  // Slide the highlight pill to the active link.
  useEffect(() => {
    const el = active ? linkRefs.current.get(active) : undefined;
    setPill(el ? { left: el.offsetLeft, width: el.offsetWidth } : null);
  }, [active, scrolled]);

  const jump = (id: string) =>
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });

  return (
    <div className="pointer-events-none sticky top-3 z-40 flex justify-center">
      <nav
        aria-label="Explore sections"
        className={`pointer-events-auto flex items-center rounded-full border bg-white/85 backdrop-blur-xl transition-all duration-500 [transition-timing-function:cubic-bezier(.22,.8,.3,1)] ${
          scrolled
            ? 'gap-0.5 border-gray-200 px-2 py-1.5 shadow-[0_16px_40px_-16px_rgba(8,6,13,.35)]'
            : 'gap-1.5 border-white/60 px-3 py-2 shadow-[0_8px_24px_-16px_rgba(8,6,13,.2)]'
        }`}
      >
        <button
          type="button"
          onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })}
          className={`flex items-center justify-center rounded-full bg-brand text-white transition-all duration-500 ${
            scrolled ? 'h-7 w-7' : 'h-8 w-8'
          }`}
          aria-label="Back to top"
        >
          <HexIcon size={scrolled ? 14 : 16} />
        </button>

        <div className="relative flex items-center">
          {pill && (
            <span
              aria-hidden
              className="absolute top-1/2 h-8 -translate-y-1/2 rounded-full bg-brand-50 transition-all duration-400 [transition-timing-function:cubic-bezier(.22,.8,.3,1)]"
              style={{ left: pill.left, width: pill.width }}
            />
          )}
          {SECTIONS.map((s) => (
            <button
              key={s.id}
              type="button"
              ref={(el) => {
                if (el) linkRefs.current.set(s.id, el);
              }}
              onClick={() => jump(s.id)}
              className={`relative rounded-full text-sm font-semibold transition-all duration-500 ${
                scrolled ? 'px-3 py-1.5' : 'px-3.5 py-2'
              } ${active === s.id ? 'text-brand' : 'text-muted hover:text-ink'}`}
            >
              {s.label}
            </button>
          ))}
        </div>

        <Link
          to="/welcome"
          className={`rounded-full bg-ink font-semibold text-white no-underline transition-all duration-500 hover:bg-ink/90 ${
            scrolled ? 'px-3.5 py-1.5 text-xs' : 'px-4 py-2 text-sm'
          }`}
        >
          Sign in
        </Link>
      </nav>
    </div>
  );
}

// --- Hero --------------------------------------------------------------------

const STAY_PHRASES = [
  'a night in Accra',
  'a semester in Kumasi',
  'a year in Tarkwa',
  'a weekend in Cape Coast',
];

/** Cycles through stay phrases; each swap re-mounts the span to replay tn-word. */
function RotatingStay() {
  const [i, setI] = useState(0);
  useEffect(() => {
    const t = setInterval(() => setI((v) => (v + 1) % STAY_PHRASES.length), 2600);
    return () => clearInterval(t);
  }, []);
  return (
    <span key={i} className="tn-word font-semibold text-emerald-200">
      {STAY_PHRASES[i]}
    </span>
  );
}

function Hero() {
  const ref = useRef<HTMLElement>(null);

  // Feed cursor position to the CSS parallax layers as -1..1 vars.
  const onMove = (e: React.MouseEvent) => {
    const el = ref.current;
    if (!el) return;
    const r = el.getBoundingClientRect();
    el.style.setProperty('--mx', String(((e.clientX - r.left) / r.width - 0.5) * 2));
    el.style.setProperty('--my', String(((e.clientY - r.top) / r.height - 0.5) * 2));
  };
  const onLeave = () => {
    ref.current?.style.setProperty('--mx', '0');
    ref.current?.style.setProperty('--my', '0');
  };

  return (
    <section
      ref={ref}
      onMouseMove={onMove}
      onMouseLeave={onLeave}
      className="tn-gradient-pan relative overflow-hidden rounded-3xl bg-[linear-gradient(120deg,#0f5132,#053b2a,#0b4d3d,#064e3b,#0f5132)] px-6 py-14 text-white sm:px-12"
    >
      {/* Ambient layers: drifting orbs + a soft dot grid, all cursor-reactive */}
      <div aria-hidden className="tn-parallax absolute inset-0 opacity-[0.14]" style={{ '--depth': -6 } as React.CSSProperties}>
        <div className="h-full w-full bg-[radial-gradient(rgba(255,255,255,.5)_1px,transparent_1px)] [background-size:26px_26px]" />
      </div>
      <div aria-hidden className="tn-parallax tn-drift pointer-events-none absolute -right-16 -top-16 h-72 w-72 rounded-full bg-emerald-400/10" style={{ '--depth': 22 } as React.CSSProperties} />
      <div aria-hidden className="tn-parallax tn-drift--slow pointer-events-none absolute -bottom-24 right-40 h-56 w-56 rounded-full bg-white/5" style={{ '--depth': 14 } as React.CSSProperties} />
      <div aria-hidden className="tn-parallax pointer-events-none absolute left-1/3 top-8 h-24 w-24 rounded-full bg-amber-300/10 blur-xl" style={{ '--depth': 30 } as React.CSSProperties} />

      {/* Floating glass booking card */}
      <div
        aria-hidden
        className="tn-parallax tn-drift--slow pointer-events-none absolute right-10 top-1/2 hidden w-64 -translate-y-1/2 lg:block"
        style={{ '--depth': 26 } as React.CSSProperties}
      >
        <div className="rounded-2xl border border-white/20 bg-white/10 p-4 shadow-2xl backdrop-blur-md">
          <div className="flex items-center gap-2.5">
            <span className="flex h-9 w-9 items-center justify-center rounded-full bg-emerald-400/90 text-ink">
              <CheckIcon size={16} />
            </span>
            <div>
              <p className="text-sm font-bold">Booking confirmed</p>
              <p className="text-xs text-white/70">Oceanview Suite · Cape Coast</p>
            </div>
          </div>
          <div className="mt-3 flex items-center justify-between rounded-xl bg-white/10 px-3 py-2 text-xs">
            <span className="text-white/80">Held in escrow</span>
            <span className="font-bold">GH₵ 540</span>
          </div>
        </div>
        <div className="mx-auto mt-3 w-fit rounded-full border border-white/20 bg-white/10 px-3 py-1.5 text-xs font-semibold backdrop-blur-md">
          <span className="mr-1.5 inline-block h-2 w-2 animate-pulse rounded-full bg-emerald-300" />
          Paid to host after move-in
        </div>
      </div>

      <div className="relative max-w-2xl">
        <Reveal>
          <p className="text-sm font-semibold uppercase tracking-widest text-emerald-200">
            Find · Stay · Thrive
          </p>
        </Reveal>
        <Reveal delay={120}>
          <h1 className="mt-3 text-3xl font-bold leading-tight sm:text-5xl">
            Renting in Ghana,
            <br />
            without the risk.
          </h1>
        </Reveal>
        <Reveal delay={240}>
          <p className="mt-4 max-w-xl text-white/80 sm:text-lg">
            Book <RotatingStay /> from a verified, identity-checked landlord — with every cedi held
            in escrow until you've moved in.
          </p>
        </Reveal>
        <Reveal delay={360}>
          <div className="mt-7 flex flex-wrap items-center gap-3">
            <Link
              to="/welcome?mode=signup&role=tenant"
              className="tn-lift inline-flex items-center gap-2 rounded-xl bg-white px-5 py-3 text-sm font-semibold text-brand no-underline transition-colors hover:bg-white/90"
            >
              Get started
            </Link>
            <Link
              to="/welcome?mode=signup&role=landlord"
              className="tn-lift inline-flex items-center gap-2 rounded-xl border border-white/40 px-5 py-3 text-sm font-semibold text-white no-underline transition-colors hover:bg-white/10"
            >
              <KeyIcon size={16} /> List your property
            </Link>
            <Link
              to="/search"
              className="inline-flex items-center gap-1.5 px-2 py-3 text-sm font-semibold text-emerald-200 no-underline transition-colors hover:text-white"
            >
              <SearchIcon size={15} /> Explore →
            </Link>
          </div>
        </Reveal>
        <Reveal delay={480}>
          <div className="mt-8 flex flex-wrap items-center gap-x-6 gap-y-2 text-sm text-white/80">
            <span className="flex items-center gap-1.5"><ShieldIcon size={14} /> Ghana Card verified hosts</span>
            <span className="flex items-center gap-1.5"><CardIcon size={14} /> Escrow-protected payments</span>
            <span className="flex items-center gap-1.5"><MapPinIcon size={14} /> Homes across Ghana</span>
            <span className="flex items-center gap-1.5"><StarIcon size={14} className="text-amber-300" /> Identity-checked community</span>
          </div>
        </Reveal>
      </div>
    </section>
  );
}

// --- Content sections ----------------------------------------------------------

const FEATURES = [
  {
    icon: <ShieldIcon size={18} />,
    chip: 'bg-brand-50 text-brand',
    title: 'Verified people & homes',
    desc: 'Hosts verify their identity with a Ghana Card and earn a TripNest ID, so you always know who you\'re renting from.',
  },
  {
    icon: <CardIcon size={18} />,
    chip: 'bg-amber-50 text-amber-600',
    title: 'Escrow-protected payments',
    desc: 'Pay with Mobile Money or card through Paystack. Your money sits in escrow and only reaches the host after your stay is confirmed.',
  },
  {
    icon: <FileIcon size={18} />,
    chip: 'bg-blue-50 text-blue-600',
    title: 'Digital agreements',
    desc: 'Rental agreements are created, signed and downloaded as PDFs right in the app — no paperwork chasing.',
  },
  {
    icon: <ChatIcon size={18} />,
    chip: 'bg-purple-50 text-purple-600',
    title: 'Chat with hosts',
    desc: 'Message landlords directly to ask questions, arrange viewings and sort out details before you commit.',
  },
  {
    icon: <ToolIcon size={18} />,
    chip: 'bg-rose-50 text-rose-500',
    title: 'Maintenance & caretakers',
    desc: 'Report issues from your phone and track them to resolution, with verified caretakers and house help on call.',
  },
  {
    icon: <UserCheckIcon size={18} />,
    chip: 'bg-teal-50 text-teal-600',
    title: 'Safety check-ins',
    desc: 'Add a trusted contact and check in on arrival, so someone you trust always knows you got there safely.',
  },
  {
    icon: <PlayIcon size={18} />,
    chip: 'bg-purple-50 text-purple-600',
    title: 'Video walkthrough',
    desc: 'Preview every room with an immersive video walkthrough generated from real listing photos — before you ever book a viewing.',
  }
];

const TENANT_STEPS = [
  { title: 'Search & compare', desc: 'Browse verified rooms, apartments and homes across Ghana — filter by city, price and amenities.' },
  { title: 'Book & pay into escrow', desc: 'Pick your dates, book instantly and pay securely. Your money is held safely, not handed over.' },
  { title: 'Move in with confidence', desc: 'Sign the digital agreement, get your receipt, and the host is paid only once you\'re settled.' },
];

const LANDLORD_STEPS = [
  { title: 'List your property', desc: 'Add photos, pricing and house rules in minutes — nightly stays or long-term rentals.' },
  { title: 'Get verified', desc: 'Confirm your identity with your Ghana Card to earn guest trust and unlock bookings.' },
  { title: 'Host & earn', desc: 'Manage bookings on your calendar and receive escrow payouts straight to Mobile Money.' },
];

function StepList({ steps }: { steps: { title: string; desc: string }[] }) {
  return (
    <ol className="space-y-4">
      {steps.map((s, i) => (
        <li key={s.title}>
          <Reveal delay={i * 120} className="flex gap-3">
            <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-brand text-xs font-bold text-white">
              {i + 1}
            </span>
            <span>
              <span className="block font-semibold text-ink">{s.title}</span>
              <span className="block text-sm text-muted">{s.desc}</span>
            </span>
          </Reveal>
        </li>
      ))}
    </ol>
  );
}

export default function ExplorePage() {
  const session = useSession();
  // Onboarding is for new visitors — signed-in users go straight to their surface.
  if (session) return <Navigate to={session.role === 'landlord' ? '/landlord' : '/'} replace />;

  return (
    <div className="flex min-h-screen flex-col bg-gray-50">
      <div className="mx-auto w-full max-w-6xl flex-1 space-y-10 px-4 pb-14 pt-3 sm:px-6">
        <NotchNav />
        <Hero />

      {/* What TripNest does */}
      <section id="features" className="scroll-mt-24">
        <Reveal className="mb-6 max-w-2xl">
          <h2 className="text-2xl font-bold text-ink">Everything a rental should come with</h2>
          <p className="mt-1 text-muted">
            TripNest isn't just listings — it's the whole stay, protected from search to move-out.
          </p>
        </Reveal>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {FEATURES.map((f, i) => (
            <Reveal key={f.title} delay={(i % 3) * 100}>
              <Card className="tn-lift h-full p-5">
                <span className={`mb-3 flex h-10 w-10 items-center justify-center rounded-xl ${f.chip}`}>
                  {f.icon}
                </span>
                <h3 className="font-bold text-ink">{f.title}</h3>
                <p className="mt-1 text-sm leading-relaxed text-muted">{f.desc}</p>
              </Card>
            </Reveal>
          ))}
        </div>
      </section>

      {/* How it works */}
      <section id="how-it-works" className="scroll-mt-24">
        <Reveal className="mb-6 max-w-2xl">
          <h2 className="text-2xl font-bold text-ink">How it works</h2>
          <p className="mt-1 text-muted">Whichever side of the door you're on.</p>
        </Reveal>
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <Reveal>
            <Card className="h-full p-6">
              <p className="mb-4 flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-brand">
                <UserIcon size={15} /> For guests & tenants
              </p>
              <StepList steps={TENANT_STEPS} />
            </Card>
          </Reveal>
          <Reveal delay={150}>
            <Card className="h-full p-6">
              <p className="mb-4 flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-brand">
                <KeyIcon size={15} /> For landlords
              </p>
              <StepList steps={LANDLORD_STEPS} />
            </Card>
          </Reveal>
        </div>
      </section>

      {/* Join CTAs */}
      <section id="join" className="grid scroll-mt-24 grid-cols-1 gap-4 lg:grid-cols-2">
        <Reveal>
          <Card className="tn-lift flex h-full flex-col items-start gap-4 border-brand/20 bg-brand-50/50 p-7">
            <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-brand text-white">
              <UserIcon size={20} />
            </span>
            <div>
              <h3 className="text-xl font-bold text-ink">Become a tenant</h3>
              <p className="mt-1 text-sm text-muted">
                Create a free account to save listings, book verified homes and pay safely with
                Mobile Money — for a night, a semester or a year.
              </p>
            </div>
            <ul className="space-y-1.5 text-sm text-ink">
              {['Book instantly on available dates', 'Money held in escrow until move-in', 'Digital agreements & receipts'].map((t) => (
                <li key={t} className="flex items-center gap-2">
                  <CheckIcon size={14} className="text-brand" /> {t}
                </li>
              ))}
            </ul>
            <Link
              to="/welcome?mode=signup&role=tenant"
              className="mt-auto inline-flex rounded-xl bg-brand px-5 py-3 text-sm font-semibold text-white no-underline transition-colors hover:bg-brand/90"
            >
              Sign up as a tenant
            </Link>
          </Card>
        </Reveal>

        <Reveal delay={150}>
          <Card className="tn-lift flex h-full flex-col items-start gap-4 border-gray-200 bg-gray-50 p-7">
            <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-ink text-white">
              <KeyIcon size={20} />
            </span>
            <div>
              <h3 className="text-xl font-bold text-ink">Become a landlord</h3>
              <p className="mt-1 text-sm text-muted">
                List rooms, apartments or whole homes. Verify once with your Ghana Card, then manage
                bookings, pricing and payouts from one dashboard.
              </p>
            </div>
            <ul className="space-y-1.5 text-sm text-ink">
              {['Free listings with your own calendar', 'Guaranteed escrow payouts after stays', 'Trust score that grows with good hosting'].map((t) => (
                <li key={t} className="flex items-center gap-2">
                  <CheckIcon size={14} className="text-brand" /> {t}
                </li>
              ))}
            </ul>
            <Link
              to="/welcome?mode=signup&role=landlord"
              className="mt-auto inline-flex rounded-xl bg-ink px-5 py-3 text-sm font-semibold text-white no-underline transition-colors hover:bg-ink/90"
            >
              Sign up as a landlord
            </Link>
          </Card>
        </Reveal>
      </section>
      </div>
      <Footer />
    </div>
  );
}
