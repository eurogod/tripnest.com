# TripNest — Frontend Integration Guide (v2, 2026-07-02)

> Audience: the frontend team (`RendoslandDev/Tripnest` → `Tripnest/Frontend`).
> Status update since v1: **every feature your UI mocks now has a live backend
> endpoint** — pricing, calendar, host tasks, team, exchange forum, resources,
> statements, tours, inquiries, payment methods included. Nothing is blocked
> on the backend. This doc tells you exactly **what to add, how, and in which
> of your files**.
>
> Backend reference: [`API.md`](../API.md) (all routes),
> [`ARCHITECTURE.md`](./ARCHITECTURE.md) (how the backend works).

## 0. The three global contracts (read first)

1. **Everything is enveloped.** Every response body is:
   ```jsonc
   { "message": "...", "statusCode": 200, "data": <payload>, "success": true }
   ```
   Unwrap `.data` once, centrally, in `client.ts`. Errors use the same shape
   (`data: null`) — surface `message`.
2. **Enums are integers**, not strings (`status: 2`). Keep one
   `src/lib/enums.ts` mapping table (provided in §5).
3. **All ids are string GUIDs.** Your `Reservation`/conversation ids typed as
   `number` must become `string`.

Paginated lists return `data: { items, totalCount, page, pageSize, totalPages }`
and accept `?page=&pageSize=` (max 100). Paged endpoints are marked ⏬ below.

## 1. `src/api/client.ts` — replace it with this

Base URL: the API serves under `/api` (dev: `http://localhost:5091/api`;
Swagger UI at `http://localhost:5091/` in Development). Set
`VITE_API_URL=http://localhost:5091/api` in `.env.development`, and proxy
`/uploads` + `/hubs` to the same origin for images and chat.

```ts
const BASE_URL = import.meta.env.VITE_API_URL ?? '/api';

interface Envelope<T> { message: string; statusCode: number; data: T | null; success: boolean; }

let accessToken: string | null = localStorage.getItem('tripnest.accessToken');
export function setAccessToken(t: string | null) {
  accessToken = t;
  t ? localStorage.setItem('tripnest.accessToken', t)
    : localStorage.removeItem('tripnest.accessToken');
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const res = await fetch(`${BASE_URL}${path}`, {
    method,
    headers: {
      ...(body !== undefined && { 'Content-Type': 'application/json' }),
      ...(accessToken && { Authorization: `Bearer ${accessToken}` }),
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  if (res.status === 401 && accessToken) {
    // access token expired → one refresh attempt, then retry once
    const ok = await tryRefresh();
    if (ok) return request<T>(method, path, body);
  }
  const envelope = (await res.json()) as Envelope<T>;
  if (!envelope.success) throw new ApiError(envelope.message, envelope.statusCode);
  return envelope.data as T;
}

export class ApiError extends Error {
  constructor(message: string, public statusCode: number) { super(message); }
}

export const apiGet    = <T>(p: string)              => request<T>('GET', p);
export const apiPost   = <T>(p: string, b?: unknown) => request<T>('POST', p, b);
export const apiPut    = <T>(p: string, b?: unknown) => request<T>('PUT', p, b);
export const apiPatch  = <T>(p: string, b?: unknown) => request<T>('PATCH', p, b);
export const apiDelete = <T>(p: string)              => request<T>('DELETE', p);
```

(`tryRefresh` lives in the auth store, §2, to avoid an import cycle — or keep
both in one module.)

## 2. `src/store/authStore.ts` — real JWT auth

Your `Session` is a localStorage stub. Replace `signIn/signOut` internals with
the real endpoints (your `useSession`/`RequireAuth` consumers can stay):

- **Register:** `POST /auth/register`
  `{ fullName, email, password, confirmPassword, phone, role }` — role is an
  **int**: 0 Tenant, 1 Landlord, 2 Agent, 3 Caretaker (Admin can't self-register).
- **Login:** `POST /auth/login` `{ email, password }` → `data` contains
  `{ userId, fullName, email, role, accessToken, refreshToken, isVerified,
  emailVerified, phoneVerified, tripNestId }`. Store both tokens; call
  `setAccessToken(accessToken)`.
- **Refresh:** `POST /auth/refresh-token` `{ refreshToken }` → new pair.
  Note: tokens are single-session — a login on another device invalidates
  this refresh token.
- **Me / logout:** `GET /auth/me`, `POST /auth/logout`.
- Extend your `Role` union: `'tenant' | 'landlord' | 'agent' | 'caretaker' | 'admin'`.

## 3. File-by-file wiring map (your `src/api/*.ts`)

Uncomment your `apiGet` lines and fix the paths/shapes as follows.
"⏬" = paginated (unwrap `.items`).

| Your file | Your call | Real endpoint | What to change |
|---|---|---|---|
| `properties.ts` | `getProperties()` | `GET /properties` | path OK; remap shape (§4) |
| | `getFeaturedProperties()` | `GET /properties/featured?limit=4` | path OK |
| | `getPropertyById(id)` | `GET /properties/{id}` | path OK |
| | `getSavedProperties()` | `GET /wishlist/mine` | **path change**; add `POST /wishlist/{propertyId}` + `DELETE /wishlist/{propertyId}` for the save toggle |
| `listings.ts` | `getListings()` | `GET /properties/user/my-properties` | **path change** (landlord's own listings) |
| `bookings.ts` | `getBookings()` | `GET /bookings/user/my-bookings` | **path change**; create = `POST /bookings` `{ propertyId, checkInDate, checkOutDate }`; cancel = `POST /bookings/{id}/cancel` (preview: `GET /bookings/{id}/cancellation-preview`) |
| `trips.ts` | `getTrips()` | `GET /bookings/user/my-bookings` | derive Trip cards from bookings (no separate trips resource) |
| `reservations.ts` | `getReservations()` | `GET /landlord/bookings` ⏬ | **path change**; ids are strings, not numbers |
| | `getReservationById(id)` | `GET /bookings/{id}` | id is a string GUID |
| `landlord.ts` | `getInquiries()` | `GET /landlord/inquiries` ⏬ | path OK; status update = `PATCH /landlord/inquiries/{id}/status` `{ status: "replied" }` |
| | `getLandlordBookings()` | `GET /landlord/bookings` ⏬ | path OK |
| | `getLandlordTenants()` | `GET /landlord/tenants` ⏬ | path OK |
| | `getLandlordReviews()` | `GET /reviews/mine` | **path change** |
| `earnings.ts` | `getEarnings()` | `GET /landlord/earnings` | path OK |
| `overview.ts` | `getOverview()` | `GET /landlord/stats` + `GET /personaldashboard/landlord` | compose your OverviewSummary from these two |
| `tenant.ts` | `getTenantDashboard()` | `GET /personaldashboard/tenant` | **path change** |
| `pricing.ts` | `getPricingSettings()` | `GET /pricing/{propertyId}` | **needs a propertyId** — pricing is per-listing, not global; add a listing selector to `PricingPage.tsx` |
| | `savePricingSettings(s)` | `PUT /pricing/{propertyId}` | same |
| `calendar.ts` | `getCalendarMonth(m)` | `GET /calendar?propertyId={id}&month=YYYY-MM` | **propertyId required** |
| `hostTasks.ts` | `getHostTasks()` | `GET /tasks` ⏬ | path OK; CRUD: `POST /tasks`, `PATCH /tasks/{id}`, `DELETE /tasks/{id}`; type/priority/status sent as **strings** ("cleaning", "high", "todo") |
| `teamUsers.ts` | `getTeamUsers()` | `GET /team` | **path change** (`/users` → `/team`); invite `POST /team`, update `PATCH /team/{id}`, remove `DELETE /team/{id}` |
| `exchange.ts` | `getExchangePosts()` | `GET /exchange/posts` ⏬ | path OK; replies: `GET/POST /exchange/posts/{postId}/replies`; create post: `POST /exchange/posts` |
| `resources.ts` | `getResources()` | `GET /resources` | path OK (POST is admin-only) |
| `statements.ts` | `getStatements()` | `GET /statements` | path OK |
| `tours.ts` | `getPropertyTour(id)` | `GET /properties/{id}/tour` | path OK (public); owners edit via `PUT` same path |
| `maintenance.ts` | `getMaintenanceTickets()` | `GET /maintenance/mine` | **path change** |
| | `createMaintenanceTicket(i)` | `POST /maintenance` | path OK |
| `messages.ts` | `getConversations()` | `GET /chat/conversations/mine` | **path change**; conversation ids are string GUIDs |
| | `getMessages(id)` | `GET /chat/conversations/{id}/messages` | **prefix `/chat`**; send = `POST .../messages`; live updates via SignalR (§6) |
| `notifications.ts` | `getNotifications()` | `GET /notifications/mine` | **path change**; also `PATCH /notifications/{id}/read`, `PATCH /notifications/mark-all-read`, `GET /notifications/unread-count` |
| `payments.ts` | `getPayments()` | `GET /receipts/mine` | **payment history = receipts**; PDF at `GET /receipts/{id}/download` |
| | `getPaymentMethods()` | `GET /payments/methods` | path OK; add `POST /payments/methods`, `PATCH .../{id}/primary`, `DELETE .../{id}` |
| | `initiatePayment(i)` | `POST /escrow/initiate` | **escrow, not direct charge**: send `{ bookingId }`, get Paystack authorization URL → redirect the user |
| | `verifyPayment(ref)` | `GET /escrow/{id}` | **no client-side verify** — Paystack's webhook confirms server-side; poll escrow/booking status after redirect back |
| `agreements.ts` | `getAgreements()` | `GET /agreements/mine` | **path change**; sign `POST /agreements/{id}/sign`, PDF `GET /agreements/{id}/download` |
| `services.ts` | `getProviders(cat)` | `GET /caretakers` + `GET /agents` | **no `/providers` endpoint** — the directory is caretakers (and agents); filter client-side, then book via `POST /caretakers/service-requests` |

Delete `src/data/*.ts` mocks module-by-module as each wire-up lands.

## 4. Shape remapping (your `types/index.ts`)

Biggest one — **Property** vs backend `PropertyResponse`:

| Yours | Backend |
|---|---|
| `id` | `propertyId` |
| `price` + `period` | `monthlyRent` + `dailyRate` (+ `stayType` enum) |
| `coords: {lat,lng}` | `latitude`, `longitude` |
| `beds`/`baths` | `bedrooms`/`bathrooms` |
| `type` | `propertyType` (int enum) |
| `amenities: string[]` | `amenities: string` — split on the delimiter |
| cover image | `photoPaths: string` (delimited); files served from `/uploads/...` |
| `agent: {...}` embedded | not embedded — `GET /agents/{id}` |
| `rating`, `reviews` | `GET /reviews/property/{id}` |
| `verified` | from verification status / `GET /trustscore/property/{id}` |

Recommended: keep your UI types, add a thin `src/lib/mappers.ts`
(`toProperty(dto)`, `toBooking(dto)`…) so DTO changes stay in one file.

## 5. Enum tables (`src/lib/enums.ts`)

Role: 0 Tenant · 1 Landlord · 2 Agent · 3 Caretaker · 4 Admin.
BookingStatus: 0 Pending · 1 Confirmed · 2 Cancelled · 3 Completed.
InquiryStatus: 0 New · 1 Replied · 2 Archived.
HostTask Type: 0 Cleaning · 1 Maintenance · 2 Inspection · 3 Restock;
Priority: 0 Low · 1 Medium · 2 High; Status: 0 Todo · 1 InProgress · 2 Done.
TeamMemberRole: 0 Owner · 1 CoHost · 2 Cleaner · 3 Maintenance · 4 Agent.
ExchangeCategory: 0 Tips · 1 Suppliers · 2 Regulation · 3 Marketplace · 4 General.
ResourceCategory: 0 Guide · 1 Policy · 2 Template · 3 Video.
StatementStatus: 0 Pending · 1 Paid.
(Full list incl. property/verification enums: `TripNest.Core/Enums/`.)

Note the asymmetry: enums arrive as **ints** in responses, but the marketplace
*write* endpoints (tasks, team, exchange, resources, inquiry status) accept
case-insensitive **string names** in request bodies — send `"cleaning"`, read `0`.

## 6. Backend features with no UI yet (please add screens)

In priority order for the product:

1. **Ghana Card verification** — the core trust feature. Flow: upload selfie +
   card number → `POST /verification/start` → poll `GET /verification/status`.
   Landlords/agents/caretakers cannot list or work until verified (writes
   return 403 with a clear message). Also email OTP (`POST /auth/email/send-otp`,
   `/verify-otp`) and phone OTP (`/auth/phone/...`) — badge states come from
   `emailVerified`/`phoneVerified`/`isVerified` on login.
2. **Escrow lifecycle UI** — status per booking (held / released / disputed),
   dispute button (`POST /escrow/{id}/dispute`), landlord release view.
3. **Walkthrough videos** — landlord uploads (`POST /properties/{id}/walkthrough`),
   status display; a listing can't go live without an approved video. Decide
   with us how this coexists with your hotspot "virtual tour" (both are live).
4. **Real-time chat** — `@microsoft/signalr` client on `/hubs/chat` with
   `?access_token=<jwt>`; REST for history.
5. **Trust/reality score** — `GET /trustscore/property/{id}` breakdown, richer
   than the boolean `verified` you render now.
6. **Safety** — trusted contact CRUD, stay check-in, panic alert (`/api/safety/*`).
7. **Smaller wins:** receipts PDF download, agreement signing + PDF,
   availability/blocked-dates editing for landlords, communication
   preferences, admin dashboard (`/admin/stats`, role 4 only).

## 7. Suggested order of work

1. `client.ts` + `authStore.ts` (§1–2) — everything depends on these.
2. Enums + mappers (§4–5).
3. Read-path wiring, module by module (§3 table, top to bottom).
4. Write paths: booking → escrow redirect → status polling.
5. New screens (§6), starting with verification.

Questions / DTO details: check `API.md` first, then ping the backend
(`TripNest.Core` repo). Swagger at the API root in dev shows every schema live.
