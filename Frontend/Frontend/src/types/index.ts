
export type ReservationStatus = 'upcoming' | 'complete' | 'canceled';


export type NavPage =
  | 'overview'
  | 'reservations'
  | 'calendar'
  | 'pricing'
  | 'statements'
  | 'listings'
  | 'tasks'
  | 'my-trips'
  | 'users'
  | 'owner-exchange'
  | 'resources';

export interface Review {
  name: string;
  date: string;
  stars: number;
  text: string;
}

export interface Reservation {
  id: string;
  property: string;
  location: string;
  status: ReservationStatus;
  checkIn: string;
  checkOut: string;
  checkInFull: string;
  checkOutFull: string;
  nights: number;
  guests: number;
  nightlyRate: number;
  managementFeePercent: number;
  tripType: string;
  bookedThrough: string;
  reviews: Review[];
}

export interface CalendarBooking {
  startDate: number;
  endDate: number;
  label: string;
}

export interface CalendarMonth {
  label: string;
  prices: Record<number, number>;
  weekendDays: number[];
  discountDays: number[];
  ownerDays: number[];
  maintenanceDays: number[];
  bookings: CalendarBooking[];
  minNights: number;
}

export interface OverviewSummary {
  monthlyEarnings: number;
  occupancyRate: number;
  upcomingCount: number;
  avgNightlyRate: number;
  recent: Reservation[];
}

export interface PricingSettings {
  baseRate: number;
  weekendRate: number;
  weeklyDiscountPercent: number;
  monthlyDiscountPercent: number;
  minNights: number;
  cleaningFee: number;
}

export interface PropertyAgent {
  name: string;
  role: string;
  phone: string;
}

//  Geographic coordinate
export interface LatLng {
  lat: number;
  lng: number;
}

export interface Property {
  id: string;
  title: string;
  location: string;
  price: number;
  period: 'month' | 'week';
  rating: number;
  reviews: number;
  verified: boolean;
  tag?: string;
  amenities: string[];
  type: string;
  beds: number;
  baths: number;
  description: string;
  agent: PropertyAgent;
//    Apartment position, used for maps, distance and routing.
  coords: LatLng;
//    Uploaded listing photo URLs (cover first); also feed the walkthrough generation.
  photos?: string[];
//    The landlord's chosen cover photo URL, if any.
  coverPhoto?: string;
}

export interface TenantDashboard {
  stats: {
    activeBookings: number;
    rentPaid: number;
    savedProperties: number;
    openMaintenance: number;
  };
  upcoming: {
    title: string;
    /** Empty when there is no upcoming stay; powers safety check-ins. */
    bookingId: string;
    propertyId: string;
    location: string;
    dates: string;
    price: number;
    period: 'month' | 'week';
    status: string;
  };
  maintenance: {
    pending: number;
    inProgress: number;
    resolved: number;
    latest: { title: string; reportedOn: string; status: string };
  };
  messages: { id: number; name: string; role: string; preview: string; time: string }[];
}

export interface Conversation {
  // number in mock data, GUID string from the live API
  id: string | number;
  name: string;
  role: string;
  lastMessage: string;
  time: string;
  unread: number;
  /** Counterpart's user id — powers live presence over the chat hub. */
  otherUserId?: string;
}

export interface ChatMessage {
  id: string | number;
  fromMe: boolean;
  text: string;
  time: string;
  /** Absolute URL of an attachment (image / voice note / document); unset for plain text. */
  mediaUrl?: string;
  /** MIME type of the attachment, e.g. image/jpeg, audio/webm, application/pdf. */
  mediaType?: string;
}

export type NotificationType = 'booking' | 'payment' | 'maintenance' | 'message' | 'safety';

export interface Notification {
  id: string | number;
  type: NotificationType;
  title: string;
  body: string;
  time: string;
  read: boolean;
}

export interface ServiceProvider {
  id: string;
  name: string;
  category: string;
  role: string;
  location: string;
  rating: number;
  reviews: number;
  verified: boolean;
  rate: number;
  ratePeriod: string;
  skills: string[];
  /** Backend user id — present on live providers; enables requests & chat. */
  userId?: string;
  bio?: string;
}

export type MaintenanceStatus = 'pending' | 'in-progress' | 'resolved';

export interface MaintenanceTicket {
  id: string | number;
  title: string;
  property: string;
  /** Backing property id — lets the ticket open a caretaker chat. */
  propertyId?: string;
  category: string;
  status: MaintenanceStatus;
  reportedOn: string;
}

export type BookingStatus = 'upcoming' | 'active' | 'past' | 'cancelled';

export interface Booking {
  id: string;
  property: string;
  propertyId: string;
  location: string;
  checkIn: string;
  checkOut: string;
  guests: number;
  amount: number;
  period: 'month' | 'week';
  status: BookingStatus;
}

/** Mirrors the backend AgreementStatus enum (Draft, Pending, Signed, Expired, Terminated).
 *  'active' is the UI name for Signed. Terminated is kept distinct from Expired: one was
 *  ended deliberately by a party, the other simply ran out. */
export type AgreementStatus = 'draft' | 'pending' | 'active' | 'expired' | 'terminated';

export interface Agreement {
  id: string;
  bookingId: string;
  property: string;
  landlord: string;
  startDate: string;
  endDate: string;
  rent: number;
  period: 'month' | 'week';
  status: AgreementStatus;
  /** The contract text the parties are bound to — shown for review before signing. */
  termsContent: string;
  /** SHA-256 of the terms captured at the first signature; recompute it over termsContent
   *  to prove the text has not changed since. Null until someone signs. */
  termsHash?: string | null;
  signedAt?: string | null;
  /** Signing is two-party: the agreement only becomes active once BOTH have signed. */
  tenantSigned: boolean;
  landlordSigned: boolean;
}

export type PaymentStatus = 'paid' | 'due' | 'upcoming';

export interface Payment {
  id: string;
  description: string;
  property: string;
  date: string;
  amount: number;
  method: string;
  status: PaymentStatus;
}

export interface PaymentMethod {
  id: string;
  provider: string;
  number: string;
  primary: boolean;
}


// Payments: provider-backed transactions (Paystack). The browser only
// initiates and reflects status — a server-side verify/webhook is the source
//  of truth. See store/paymentStore.ts for the mock implementation.


export type PaymentChannel = 'momo' | 'card';
export type TransactionStatus = 'pending' | 'success' | 'failed' | 'refunded';

export interface Transaction {
  reference: string;
  bookingId?: string;
  amount: number;
  currency: 'GHS';
  channel: PaymentChannel;
  provider: 'paystack';
  status: TransactionStatus;
  createdAt: string;
}


//  Landlord earnings & payouts (settlement of successful transactions).


export type EarningStatus = 'settled' | 'pending' | 'processing' | 'failed';

export interface EarningTxn {
  id: string;
  date: string;
  guest: string;
  listing: string;
  gross: number;
  fee: number;
  net: number;
  status: EarningStatus;
}

export interface EarningsSummary {
  available: number;
  pending: number;
  thisMonth: number;
  lastMonth: number;
  lifetime: number;
  nextPayoutDate: string;
  transactions: EarningTxn[];
}

/** Where the host's payouts are sent (account number arrives masked). */
export interface PayoutAccount {
  channel: 'mobile_money' | 'ghipss';
  providerCode: string;
  accountNumber: string;
  accountName: string;
  providerRegistered: boolean;
}

//  Landlord workspace: inquiries, bookings, tenants, reviews.


export type InquiryStatus = 'new' | 'replied' | 'archived';

export interface Inquiry {
  id: string;
  guest: string;
  listing: string;
  message: string;
  date: string;
  status: InquiryStatus;
}

export type LandlordBookingStatus = 'pending' | 'confirmed' | 'checked-in' | 'completed' | 'cancelled';

export interface LandlordBooking {
  id: string;
  guest: string;
  listing: string;
  checkIn: string;
  checkOut: string;
  nights: number;
  guests: number;
  amount: number;
  status: LandlordBookingStatus;
}

export type TenantStanding = 'current' | 'overdue' | 'ending-soon';

export interface LandlordTenant {
  id: string;
  name: string;
  property: string;
  email: string;
  phone: string;
  since: string;
  leaseEnd: string;
  monthlyRent: number;
  standing: TenantStanding;
}

export interface LandlordReview {
  id: string;
  guest: string;
  listing: string;
  rating: number;
  date: string;
  text: string;
  reply?: string;
}

export type StatementStatus = 'paid' | 'pending';

export interface Statement {
  id: string;
  month: string;
  period: string;
  grossRevenue: number;
  managementFee: number;
  netPayout: number;
  status: StatementStatus;
}

//  Host dashboard: Listings


export type ListingStatus = 'published' | 'unlisted' | 'draft' | 'snoozed';

export interface ListingPhoto {
  id: string;
  url: string;
  isCover: boolean;
  sortOrder: number;
}

export interface Listing {
  id: string;
  title: string;
  location: string;
  type: string;
  status: ListingStatus;
  nightlyRate: number;
  beds: number;
  baths: number;
  occupancyRate: number;
  rating: number;
  reviews: number;
  coverColor: string;
  /** Uploaded photos from the backend (cover first). Empty when none uploaded yet. */
  photos: ListingPhoto[];
  /** The landlord's chosen cover photo URL, if any. */
  coverPhoto?: string;
  /** Long-form description (present on detail fetches). */
  description?: string;
  /** Amenity labels, parsed from the stored CSV. */
  amenities: string[];
}

//  Host dashboard: Tasks


export type TaskType = 'cleaning' | 'maintenance' | 'inspection' | 'restock';
export type TaskPriority = 'low' | 'medium' | 'high';
export type TaskStatus = 'todo' | 'in-progress' | 'done';

export interface HostTask {
  id: string;
  title: string;
  property: string;
  type: TaskType;
  priority: TaskPriority;
  status: TaskStatus;
  dueDate: string;
  assignee: string;
}

//  Host dashboard: My Trips


export type TripStatus = 'upcoming' | 'completed' | 'canceled';

export interface Trip {
  id: string;
  propertyId: string;
  destination: string;
  property: string;
  checkIn: string;
  checkOut: string;
  nights: number;
  guests: number;
  price: number;
  status: TripStatus;
  coverColor: string;
}

// Host dashboard: Users


export type TeamRole = 'owner' | 'co-host' | 'cleaner' | 'maintenance' | 'agent';
export type TeamUserStatus = 'active' | 'invited' | 'suspended';

export interface TeamUser {
  id: string;
  name: string;
  email: string;
  role: TeamRole;
  status: TeamUserStatus;
  initials: string;
  lastActive: string;
  properties: number;
}

//  Host dashboard: Owner Exchange


export type ExchangeCategory = 'Tips' | 'Suppliers' | 'Regulation' | 'Marketplace' | 'General';

export interface ExchangePost {
  id: string;
  author: string;
  role: string;
  initials: string;
  title: string;
  body: string;
  category: ExchangeCategory;
  replies: number;
  createdAt: string;
  pinned: boolean;
}

export interface ExchangeReply {
  id: string;
  author: string;
  initials: string;
  body: string;
  createdAt: string;
}

//  Host dashboard: Resources


export type ResourceCategory = 'guide' | 'policy' | 'template' | 'video';

export interface Resource {
  id: string;
  title: string;
  description: string;
  category: ResourceCategory;
  format: string;
  url: string;
}

// Tenant marketplace: Virtual Tour


export type HotspotCategory =
  | 'bed'
  | 'seating'
  | 'kitchen'
  | 'bathroom'
  | 'storage'
  | 'entertainment'
  | 'view'
  | 'outdoor'
  | 'amenity'
  | 'workspace'
  | 'parking';

//  A clickable point of interest pinned onto a tour scene (percent coords)
export interface TourHotspot {
  id: string;
  x: number; // 0–100, % from left
  y: number; // 0–100, % from top
  label: string;
  category: HotspotCategory;
  detail: string;
}

export type TourVideoStatus = 'pending' | 'ready' | 'failed';

//  A walkthrough video generated from listing photos (e.g. Google Flow / Veo).
//    Carries generation metadata so in-app generation can be added later.
export interface TourVideo {
  url?: string; // playable when status === 'ready'
  poster?: string;
  durationSec?: number; // authored hint; runtime prefers the loaded video's duration
  status: TourVideoStatus;
  provider?: 'google-flow' | 'local'; // local = free in-browser Ken Burns render
  sourcePhotos?: string[]; // photos the clip was generated from
  generatedAt?: string; // ISO timestamp
}

//  A room's position inside the continuous walkthrough video.
export interface TourChapter {
  roomId: string; // matches a TourRoom.id
  startSec: number; // end is the next chapter's start (or the video's end)
}

//  One clip in a synthesized playlist full video.
export interface TourSegment {
  roomId: string;
  url: string;
  durationSec: number; // nominal; runtime corrects from loadedmetadata
}

export interface TourFullVideo extends TourVideo {
  chapters: TourChapter[];
//    When present, the full video is a sequential playlist of these clips
//    (synthesized from per-room generated clips); `url` is unused.
  segments?: TourSegment[];
}

//  One stop on the walkthrough — rendered as a video clip when one is ready,
//    else a still image, else a cinematic gradient placeholder.
export interface TourRoom {
  id: string;
  name: string;
  area: 'Entrance' | 'Indoor' | 'Outdoor' | 'Exterior';
  caption: string;
  dimensions?: string;
//    Gradient stops for the placeholder scene (hex).
  from: string;
  to: string;
//    Optional still image URL; used when no ready clip exists.
  image?: string;
//    Optional generated walkthrough clip for this room.
  clip?: TourVideo;
  hotspots: TourHotspot[];
}

export interface PropertyTour {
  propertyId: string;
  title: string;
  rooms: TourRoom[];
//    Optional continuous walkthrough video with per-room chapters.
  fullVideo?: TourFullVideo;
}
