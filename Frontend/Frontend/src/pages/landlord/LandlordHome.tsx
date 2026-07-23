import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import type { Conversation, EarningsSummary, Listing, ListingStatus } from '../../types';
import { getListings } from '../../api/listings';
import { getEarnings } from '../../api/earnings';
import { getConversations } from '../../api/messages';
import { getCachedListingPhotos } from '../../lib/listingPhotos';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import Badge, { type BadgeTone } from '../../components/ui/Badge';
import Avatar from '../../components/ui/Avatar';
import AddListingModal from '../../components/landlord/AddListingModal';
import EditListingModal from '../../components/landlord/EditListingModal';
import { formatCedi } from '../../lib/format';
import { useSession } from '../../store/authStore';
import {
  KeyIcon, CardIcon, CalendarIcon, StarIcon, MapPinIcon, ChevronRightIcon,
  MessageIcon, BadgeIcon, MailIcon, UsersIcon, FileIcon,
} from '../../components/tenant/icons';

const STATUS: Record<ListingStatus, { tone: BadgeTone; label: string }> = {
  published: { tone: 'green', label: 'Published' },
  unlisted: { tone: 'gray', label: 'Unlisted' },
  draft: { tone: 'amber', label: 'Draft' },
  snoozed: { tone: 'blue', label: 'Snoozed' },
};

interface OverviewData {
  listings: Listing[];
  /** null → earnings unavailable (e.g. 403 while unverified). */
  earnings: EarningsSummary | null;
  /** null → chat fetch failed. */
  conversations: Conversation[] | null;
}

// Listings are the page's backbone; earnings and chat degrade to fallbacks.
async function loadOverview(): Promise<OverviewData> {
  const [listings, earnings, conversations] = await Promise.allSettled([
    getListings(), getEarnings(), getConversations(),
  ]);
  if (listings.status === 'rejected') throw listings.reason;
  return {
    listings: listings.value,
    earnings: earnings.status === 'fulfilled' ? earnings.value : null,
    conversations: conversations.status === 'fulfilled' ? conversations.value : null,
  };
}

function StatCard({ to, icon, label, value, sub, index }: {
  to: string;
  icon: React.ReactNode;
  label: string;
  value: string;
  sub?: React.ReactNode;
  index: number;
}) {
  return (
    <Link to={to} className="no-underline">
      <Card
        className="tn-card-in tn-lift group h-full p-5"
        style={{ animationDelay: `${index * 60}ms` }}
      >
        <div className="flex items-start justify-between">
          <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-brand-50 text-brand">
            {icon}
          </span>
          <ChevronRightIcon size={16} className="text-gray-300 transition-colors group-hover:text-brand" />
        </div>
        <p className="mt-3 text-2xl font-bold tracking-tight text-ink">{value}</p>
        <p className="mt-0.5 text-sm text-muted">{label}</p>
        {sub && <p className="mt-1 text-xs">{sub}</p>}
      </Card>
    </Link>
  );
}

/** ▲/▼ month-over-month movement for the earnings stat. */
function EarningsDelta({ earnings }: { earnings: EarningsSummary }) {
  const diff = earnings.thisMonth - earnings.lastMonth;
  if (diff === 0) return <span className="text-muted">Same as last month</span>;
  return diff > 0 ? (
    <span className="font-semibold text-brand">▲ {formatCedi(diff)} vs last month</span>
  ) : (
    <span className="font-semibold text-rose-600">▼ {formatCedi(-diff)} vs last month</span>
  );
}

function OverviewListingCard({ listing, onEdit }: { listing: Listing; onEdit: (id: string) => void }) {
  const navigate = useNavigate();
  const status = STATUS[listing.status];
  const photo = listing.coverPhoto ?? getCachedListingPhotos(listing.id)?.[0];
  // Published listings are read-only — open the detail/gallery; drafts open the editor.
  const published = listing.status === 'published';
  const open = () => (published ? navigate(`/landlord/listings/${listing.id}`) : onEdit(listing.id));
  return (
    <button
      type="button"
      onClick={open}
      aria-label={`${published ? 'View' : 'Edit'} ${listing.title}`}
      className="group w-full text-left"
    >
      <Card className="tn-lift h-full overflow-hidden">
        <div className="relative h-28" style={{ backgroundColor: listing.coverColor }}>
          {photo && <img src={photo} alt="" className="h-full w-full object-cover" />}
          <Badge tone={status.tone} className="absolute left-3 top-3">{status.label}</Badge>
        </div>
        <div className="p-4">
          <h3 className="truncate font-semibold text-ink">{listing.title}</h3>
          <p className="flex items-center gap-1 text-sm text-muted">
            <MapPinIcon size={13} /> {listing.location}
          </p>
          <div className="mt-3 flex items-end justify-between">
            <p className="font-bold text-brand">
              {formatCedi(listing.nightlyRate)}
              <span className="text-xs font-normal text-muted"> / night</span>
            </p>
            <span className="text-xs text-muted">{listing.beds} bd · {listing.baths} ba</span>
          </div>
          <p className="mt-2 flex items-center gap-1 text-xs font-semibold text-brand opacity-0 transition-opacity group-hover:opacity-100">
            {published ? 'View details' : 'Edit details'} <ChevronRightIcon size={12} />
          </p>
        </div>
      </Card>
    </button>
  );
}

function RecentMessagesCard({ conversations }: { conversations: Conversation[] | null }) {
  return (
    <Card className="p-5">
      <div className="mb-3 flex items-center justify-between">
        <h3 className="font-bold text-ink">Recent messages</h3>
        <Link to="/landlord/messages" className="text-xs font-semibold text-brand no-underline">
          View all
        </Link>
      </div>
      {conversations === null ? (
        <p className="text-sm text-muted">Messages are unavailable right now.</p>
      ) : conversations.length === 0 ? (
        <p className="text-sm text-muted">No messages yet. Tenants who inquire will appear here.</p>
      ) : (
        <ul className="-mx-2 space-y-1">
          {conversations.slice(0, 4).map((c) => (
            <li key={c.id}>
              <Link
                to={`/landlord/messages/${c.id}`}
                className="flex items-center gap-3 rounded-lg px-2 py-2 no-underline transition-colors hover:bg-gray-50"
              >
                <Avatar name={c.name} size={36} />
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-semibold text-ink">{c.name}</span>
                  <span className={`block truncate text-xs ${c.unread > 0 ? 'font-medium text-ink' : 'text-muted'}`}>
                    {c.lastMessage || 'Start the conversation'}
                  </span>
                </span>
                {c.unread > 0 ? (
                  <span className="flex h-5 min-w-5 shrink-0 items-center justify-center rounded-full bg-brand px-1.5 text-[11px] font-semibold text-white">
                    {c.unread}
                  </span>
                ) : (
                  <span className="shrink-0 text-xs text-muted">{c.time}</span>
                )}
              </Link>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}

function EarningsCard({ earnings }: { earnings: EarningsSummary | null }) {
  return (
    <Card className="p-5">
      <h3 className="font-bold text-ink">This month's earnings</h3>
      {earnings === null ? (
        <p className="mt-2 text-sm text-muted">
          Earnings will appear once your account is verified.
        </p>
      ) : (
        <>
          <p className="mt-2 text-3xl font-bold text-brand">{formatCedi(earnings.thisMonth)}</p>
          <div className="mt-4 space-y-2.5">
            {([
              ['This month', earnings.thisMonth, 'bg-brand'],
              ['Last month', earnings.lastMonth, 'bg-gray-300'],
            ] as const).map(([label, value, fill]) => {
              const max = Math.max(earnings.thisMonth, earnings.lastMonth, 1);
              return (
                <div key={label} className="flex items-center gap-3 text-xs">
                  <span className="w-20 shrink-0 text-muted">{label}</span>
                  <span className="h-2 flex-1 overflow-hidden rounded-full bg-gray-100">
                    <span
                      className={`block h-full rounded-full ${fill} transition-[width] duration-500`}
                      style={{ width: `${(value / max) * 100}%` }}
                    />
                  </span>
                  <span className="w-20 shrink-0 text-right font-semibold text-ink">{formatCedi(value)}</span>
                </div>
              );
            })}
          </div>
        </>
      )}
      <Link to="/landlord/earnings" className="no-underline">
        <Button variant="ghost" size="sm" className="mt-4 gap-1">
          Earnings breakdown <ChevronRightIcon size={14} />
        </Button>
      </Link>
    </Card>
  );
}

const QUICK_ACTIONS = [
  { label: 'Calendar', to: '/landlord/calendar', icon: <CalendarIcon size={16} /> },
  { label: 'Bookings', to: '/landlord/bookings', icon: <FileIcon size={16} /> },
  { label: 'Pricing', to: '/landlord/pricing', icon: <CardIcon size={16} /> },
  { label: 'Inquiries', to: '/landlord/inquiries', icon: <MailIcon size={16} /> },
  { label: 'Reviews', to: '/landlord/reviews', icon: <StarIcon size={16} /> },
  { label: 'Tenants', to: '/landlord/tenants', icon: <UsersIcon size={16} /> },
];

function QuickActionsCard() {
  return (
    <Card className="p-5">
      <h3 className="mb-3 font-bold text-ink">Quick actions</h3>
      <div className="grid grid-cols-2 gap-2">
        {QUICK_ACTIONS.map((a) => (
          <Link
            key={a.to}
            to={a.to}
            className="flex items-center gap-2 rounded-lg border border-gray-100 px-3 py-2 text-sm font-medium text-gray-700 no-underline transition-colors hover:border-brand-50 hover:bg-brand-50/50 hover:text-brand"
          >
            <span className="text-brand">{a.icon}</span> {a.label}
          </Link>
        ))}
      </div>
    </Card>
  );
}

function Home({ data }: { data: OverviewData }) {
  const session = useSession();
  const firstName = (session?.name ?? 'there').split(' ')[0];
  const [listings, setListings] = useState(data.listings);
  const [editingId, setEditingId] = useState<string | null>(null);
  const { earnings, conversations } = data;

  const published = listings.filter((l) => l.status === 'published').length;
  const unread = conversations?.reduce((sum, c) => sum + c.unread, 0);

  return (
    <div className="space-y-6">
      <div className="tn-rise flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight text-ink">Welcome back, {firstName}</h1>
          <p className="mt-1 text-muted">Here's how your properties are performing.</p>
        </div>
        <AddListingModal
          triggerLabel="+ List a property"
          onCreated={(l) => setListings((ls) => [l, ...ls])}
        />
      </div>

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard
          index={0}
          to="/landlord/listings"
          icon={<KeyIcon size={18} />}
          label="Active listings"
          value={`${published}/${listings.length}`}
          sub={<span className="text-muted">{listings.length - published} not yet live</span>}
        />
        <StatCard
          index={1}
          to="/landlord/earnings"
          icon={<CardIcon size={18} />}
          label="This month"
          value={earnings ? formatCedi(earnings.thisMonth) : '—'}
          sub={earnings ? <EarningsDelta earnings={earnings} /> : <span className="text-muted">Not available yet</span>}
        />
        <StatCard
          index={2}
          to="/landlord/earnings"
          icon={<BadgeIcon size={18} />}
          label="Total earned"
          value={earnings ? formatCedi(earnings.lifetime) : '—'}
          sub={<span className="text-muted">All-time payouts</span>}
        />
        <StatCard
          index={3}
          to="/landlord/messages"
          icon={<MessageIcon size={18} />}
          label="Unread messages"
          value={unread === undefined ? '—' : String(unread)}
          sub={
            conversations ? (
              <span className="text-muted">{conversations.length} conversation{conversations.length === 1 ? '' : 's'}</span>
            ) : (
              <span className="text-muted">Not available yet</span>
            )
          }
        />
      </div>

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-[1fr_360px]">
        <section className="min-w-0">
          <div className="mb-4 flex items-center justify-between">
            <h2 className="text-xl font-bold text-ink">Your listings</h2>
            <Link to="/landlord/listings" className="text-sm font-semibold text-brand no-underline">
              {listings.length > 6 ? `View all ${listings.length}` : 'View all'}
            </Link>
          </div>
          {listings.length === 0 ? (
            <Card className="border-dashed p-10 text-center">
              <p className="font-semibold text-ink">No listings yet</p>
              <p className="mt-1 text-sm text-muted">
                Add your first property with “List a property” above to start earning.
              </p>
            </Card>
          ) : (
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {listings.slice(0, 6).map((l) => (
                <OverviewListingCard key={l.id} listing={l} onEdit={setEditingId} />
              ))}
            </div>
          )}
        </section>

        <aside className="min-w-0 space-y-5">
          <RecentMessagesCard conversations={conversations} />
          <EarningsCard earnings={earnings} />
          <QuickActionsCard />

          <Card className="border-ink! bg-ink! p-5 text-white">
            <h3 className="font-bold">Grow your portfolio</h3>
            <p className="mt-1 text-sm text-white/70">
              Verified listings get 3× more inquiries. Add your next property today.
            </p>
            <Link to="/landlord/listings" className="no-underline">
              <Button className="mt-3 rounded-xl bg-white! text-ink! hover:bg-white/90!" size="sm">
                List a property
              </Button>
            </Link>
          </Card>
        </aside>
      </div>

      {editingId && (
        <EditListingModal
          listingId={editingId}
          onClose={() => setEditingId(null)}
          onUpdated={(updated) => {
            setListings((ls) => ls.map((l) => (l.id === updated.id ? updated : l)));
            setEditingId(null);
          }}
        />
      )}
    </div>
  );
}

export default function LandlordHome() {
  const state = useAsync(loadOverview, []);

  return (
    <AsyncBoundary
      state={state}
      loadingMessage="Loading your portfolio…"
      errorMessage="Failed to load your portfolio."
    >
      {(data) => <Home data={data} />}
    </AsyncBoundary>
  );
}
