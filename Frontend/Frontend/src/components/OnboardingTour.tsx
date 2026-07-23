import GuidedTour, { type TourStep } from './GuidedTour';
import { useSession } from '../store/authStore';

// First-time-user walkthroughs, one per role. Shown once per user per
// browser; skipping counts as seen.

const TENANT_STEPS: TourStep[] = [
  {
    title: 'Welcome to TripNest 👋',
    body: 'Find verified homes across Ghana with identity-checked hosts and escrow-protected payments. Here’s a quick look around.',
  },
  {
    selector: 'a[href="/search"]',
    title: 'Find your next stay',
    body: 'Search and filter verified listings, view them on a map under Nearby, and save favourites for later.',
  },
  {
    selector: 'a[href="/bookings"]',
    title: 'Your bookings',
    body: 'Track upcoming and past stays here — check-in details, agreements and cancellations all live in one place.',
  },
  {
    selector: 'a[href="/payments"]',
    title: 'Payments, protected',
    body: 'Pay securely through Paystack. Your money sits in escrow and is only released to the host once your stay is confirmed.',
  },
  {
    selector: 'a[href="/profile"]',
    title: 'Get verified',
    body: 'Verify your email and phone on your profile — a verified account builds trust with hosts and unlocks the full experience.',
  },
];

const LANDLORD_STEPS: TourStep[] = [
  {
    title: 'Welcome to your hosting workspace 👋',
    body: 'Everything you need to run your properties — listings, bookings, earnings and payouts. Here’s a quick look around.',
  },
  {
    selector: 'a[href="/landlord/listings"]',
    title: 'List your properties',
    body: 'Create listings, upload photos, and generate walkthrough videos. Listings go live once TripNest verifies them.',
  },
  {
    selector: 'a[href="/landlord/bookings"]',
    title: 'Incoming bookings',
    body: 'Guest bookings appear here as payments land. You can decline a booking any time — the guest is always refunded in full.',
  },
  {
    selector: 'a[href="/landlord/earnings"]',
    title: 'Earnings & payouts',
    body: 'Register your MoMo wallet or bank account and payouts are sent automatically when a stay completes.',
  },
  {
    selector: 'a[href="/landlord/settings"]',
    title: 'Verify your identity',
    body: 'Hosts must verify with their Ghana Card before listing. Head to Settings to complete verification.',
  },
];

export default function OnboardingTour({ role }: { role: 'tenant' | 'landlord' }) {
  const session = useSession();
  if (!session || session.role !== role) return null;
  return (
    <GuidedTour
      steps={role === 'tenant' ? TENANT_STEPS : LANDLORD_STEPS}
      storageKey={`tripnest.tour.${role}.${session.userId}`}
    />
  );
}
