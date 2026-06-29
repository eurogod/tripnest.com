# TripNest — Frontend ↔ Backend Integration Handoff

> Audience: the frontend team (`RendoslandDev/Tripnest` → `Tripnest/Frontend`).
> Purpose: (1) get the app off mock data and onto the live API, (2) align data
> shapes with the real DTOs, (3) list backend capabilities the UI hasn't wired
> yet, (4) flag UI features that have **no** backend so we can plan them.

The backend (`TripNest.Core`, ASP.NET 8) is live and exposes **28 controllers**.
All responses are wrapped in an envelope:

```jsonc
{ "success": true, "message": "...", "data": <payload>, "errors": [] }
```

**Enums serialize as integers** (e.g. `status: 2`), not strings. Unwrap `.data`
in the client.

---

## 1. Current state of the frontend

- 100% mock-backed. Every `src/api/*.ts` returns `mockResponse(localData)`; the
  real `apiGet(...)` call is commented out.
- `client.ts` only implements `apiGet` — no POST/PUT/PATCH/DELETE, no auth header.
- `authStore.ts` is **localStorage only**: no JWT, no token refresh.
- `types/index.ts` types are bespoke and **do not match** the backend DTOs.
- Auth role is only `'tenant' | 'landlord'`. Backend supports **tenant,
  landlord, agent, caretaker, admin**.

## 2. Wiring checklist (frontend work, backend already supports)

1. Set `VITE_API_URL` to the backend origin; proxy `/api`, `/uploads`, `/hubs`.
2. Extend `client.ts` with `apiPost/apiPut/apiPatch/apiDelete` and an
   `Authorization: Bearer <token>` header.
3. Replace localStorage auth with real JWT:
   - `POST /api/auth/register`, `POST /api/auth/login` → returns access + refresh token + user.
   - `POST /api/auth/refresh-token`, `GET /api/auth/me`, `POST /api/auth/logout`.
   - `POST /api/auth/change-password | forgot-password | reset-password`.
4. Unwrap the `{ success, data }` envelope once, centrally.
5. Uncomment the real calls in each `api/*.ts` and remap shapes (section 3).

## 3. Shape alignment — their type → our DTO

**Property** (`GET /api/properties`, `/api/properties/{id}`, `/search`):

| Frontend `Property`        | Backend `PropertyResponse`                       |
|----------------------------|--------------------------------------------------|
| `id`                       | `propertyId`                                     |
| `price` + `period`         | `monthlyRent` + `dailyRate` (+ `stayType` enum)  |
| `coords: {lat,lng}`        | `latitude`, `longitude`                          |
| `beds` / `baths`           | `bedrooms` / `bathrooms`                         |
| `type`                     | `propertyType`                                   |
| `amenities: string[]`      | `amenities: string` (delimited — split client-side) |
| (cover image)              | `photoPaths: string` (delimited)                 |
| `agent: {...}` (embedded)  | **not on property** — fetch separately           |
| `rating`, `reviews`        | **not on property** — from `/api/reviews/property/{id}` |
| `verified`                 | derived from verification + `/api/trustscore`    |
| —                          | `status` (enum), `cancellationPolicy` (enum)     |

**Booking**: create with `POST /api/bookings`
`{ propertyId, checkInDate, checkOutDate }` → `BookingResponse`. List via
`GET /api/bookings/user/my-bookings`; cancel `POST /api/bookings/{id}/cancel`
(preview refund at `/{id}/cancellation-preview`). Payment is **escrow**, not a
direct charge (section 4).

Other mismatched modules to remap: `bookings`, `payments`, `messages`,
`notifications`, `maintenance`, `agreements`, `trips`, `reservations`,
`earnings`, `listings`, `services`.

## 4. Backend capabilities the UI hasn't integrated yet

These are **built and live** — please add UI for them:

- **Identity / verification (core to the product).** Ghana Card verification via
  the separate TripNest.Id service: `POST /api/verification/start`,
  `GET /api/verification/status`. Plus email OTP (`/api/auth/email/*`) and phone
  OTP (`/api/auth/phone/*`). The `verified` badge depends on this. Hosts,
  agents and caretakers **must** verify before listing/working.
- **Walkthrough videos.** `POST /api/properties/{id}/walkthrough` (video upload),
  admin/agent review queue (`/pending-walkthroughs`, `.../walkthrough/review`).
  A listing can't go live without an **approved walkthrough video**. Note: this
  replaces the mock "VirtualTour" hotspot concept — confirm which we ship.
- **Trust / reality score.** `GET /api/trustscore/...` — the advanced reality
  score (verification + history + feedback breakdown). Today the UI only has a
  boolean `verified` and a star rating.
- **Escrow payments.** `POST /api/escrow/initiate`, `webhook`, `{id}/release`,
  `{id}/dispute`, `{id}/resolve-dispute`, `{id}/refund`. Paystack/GHS — the
  browser only initiates; server verify/webhook is source of truth. Replace the
  `paymentStore` mock with the full escrow lifecycle.
- **Safety.** `/api/safety/contact`, `/checkin`, `/alert` (panic). The UI has a
  static `emergencyContact` string — wire the real endpoints.
- **Agents & caretakers as first-class roles.** Viewing requests
  (`/api/agents/{id}/viewing-requests`), service requests + assignment
  (`/api/caretakers/service-requests/*`), provider reviews. Extend auth roles
  beyond tenant/landlord.
- **Real-time chat.** SignalR hub + `/api/chat/conversations/*` and
  `/messages`. Replace mock messages; subscribe to the hub for live updates.
- **Also live:** agreements (sign + PDF download), receipts (download, by
  booking), notifications (read/unread/mark-all), availability + blocked-dates,
  communication preferences, per-role dashboards
  (`/api/dashboard/{tenant|landlord|agent|caretaker}`), profile + ID card,
  wishlist, settings, config `app-info`.

## 5. Frontend features with NO backend (decision needed)

These pages exist in the UI but have **no endpoint**. We either build the API,
map them onto an existing one, or defer:

| UI feature            | Backend status                                  |
|-----------------------|-------------------------------------------------|
| Owner Exchange (forum)| None — new service needed if we keep it         |
| Host Tasks board      | None — only `/api/maintenance` exists           |
| Pricing settings (weekend/discount/cleaning fee) | None — property has flat `monthlyRent`/`dailyRate` |
| Team Users (co-hosts/cleaners) | None                                   |
| Resources (guides/templates) | None                                     |
| Monthly Statements    | None — closest is receipts + landlord earnings  |
| Calendar owner/maintenance/discount day overlays | Partial — via availability blocked-dates |

## 6. Recommended sequence

1. **Contract first:** adopt the backend DTO shapes in `types/index.ts` (or add a
   mapping layer). Align enums to integers.
2. **Auth + client:** real JWT, full verb set, central envelope unwrap.
3. **Read paths:** properties, search, dashboards, bookings, messages, notifications.
4. **Write paths:** booking → escrow, reviews, maintenance, walkthrough upload.
5. **Trust layer:** verification flow, trust/reality score, safety.
6. **Triage section 5** — decide build vs. defer per feature.
