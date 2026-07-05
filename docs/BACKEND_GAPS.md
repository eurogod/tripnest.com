# Backend Gaps vs. Frontend — What I Still Need to Build

> ✅ **STATUS (resolved): all 13 gaps below have now been built** — entities, EF config,
> migration `AddMarketplaceModules`, services, and controllers. Endpoints are listed in
> `API.md`. Build is clean and all 168 tests pass. The sections below are kept as the
> design record / rationale. New routes: `api/pricing`, `api/calendar`, `api/landlord/{bookings,tenants,inquiries}`,
> `api/inquiries`, `api/payments/methods`, `api/tasks`, `api/team`, `api/statements`,
> `api/exchange`, `api/resources`, `api/properties/{id}/tour`, `api/properties/featured`.


> For the backend owner. Derived from a full read of the frontend
> (`RendoslandDev/Tripnest` → `Tripnest/Frontend`): every `src/api/*.ts`, its
> commented `apiGet/apiPost/apiPut` target, and its data shape.
>
> **Answer to "do I have all of it?" → No.** Your backend already covers the
> core (auth, properties, bookings, maintenance, chat, notifications, wishlist,
> reviews, per-role dashboards, escrow, verification, walkthrough, trust, safety
> — and in fact *more* than the UI uses). But there are **13 things the UI
> fetches that have no matching endpoint.** They split into: 2 tiny adds,
> 4 partial/extend, 7 brand-new modules.

## Already covered — frontend just needs to wire (no backend work)

| Frontend call            | Your endpoint                                   |
|--------------------------|-------------------------------------------------|
| `GET /properties`        | `GET /api/properties` (GetAllActiveProperties)  |
| `GET /properties/{id}`   | `GET /api/properties/{propertyId}`              |
| search                   | `GET /api/properties/search`                    |
| `GET /properties/saved`  | `GET /api/wishlist/mine`                         |
| `GET /listings`          | `GET /api/properties/user/my-properties`        |
| `GET /bookings`          | `GET /api/bookings/user/my-bookings`            |
| `GET /trips`             | `GET /api/bookings/user/my-bookings`            |
| `GET /maintenance` (+POST)| `GET/POST /api/maintenance`                    |
| `GET /conversations` (+msgs)| `GET /api/chat/conversations/*`              |
| `GET /notifications`     | `GET /api/notifications/mine`                    |
| `GET /tenant/dashboard`  | `GET /api/dashboard/tenant`                      |
| `GET /overview`          | `GET /api/dashboard/landlord` (or personal)     |
| `GET /landlord/earnings` | `GET /api/landlord/earnings`                     |
| `GET /landlord/reviews`  | `GET /api/reviews/user/{userId}`                |
| `GET /providers?category`| `GET /api/agents`, `GET /api/caretakers`        |

---

## The 13 gaps (what I need to build)

### A. Tiny adds (hours)

**1. Featured properties** — `GET /properties/featured`
- UI: `properties.ts → getFeaturedProperties()`, home page hero grid.
- You have `GetAllActiveProperties`. Add a `featured` flag or just return top-N
  by rating/recency. ~1 endpoint.

**2. Browse without a search term**
- UI lists all properties then filters client-side. Your `/search` *requires*
  `location` + bed range. `GetAllActiveProperties` already covers this — just
  confirm it's reachable at `GET /api/properties` with optional filters
  (price, type, amenities) so the UI's filter bar maps cleanly.

### B. Partial — extend what exists (days)

**3. Pricing calendar** — `GET /calendar?month=`
- UI: `calendar.ts`, `CalendarPage`. Shape (`CalendarMonth`): per-day `prices`,
  `weekendDays`, `discountDays`, `ownerDays`, `maintenanceDays`, `bookings`,
  `minNights`.
- You have `availability` + `blocked-dates` (block only). Missing: **per-day
  pricing** and day-type overlays. Extend Availability to return a priced,
  typed calendar.

**4. Landlord reservations / incoming bookings** — `/landlord/bookings`, `/reservations`
- UI: `landlord.ts → getLandlordBookings()`, `reservations.ts`. Shape: guest,
  listing, check-in/out, nights, status (`pending|confirmed|checked-in|completed|cancelled`),
  management-fee %, reviews on the reservation.
- You expose tenant `my-bookings` but **not the landlord-side list** with
  check-in status. Add `GET /api/landlord/bookings` (+ a reservation detail
  with the management-fee/review fields).

**5. Landlord inquiries** — `GET /landlord/inquiries`
- UI: pre-booking guest questions with `new|replied|archived`. Closest is chat,
  but there's no inquiry model/status. Either model inquiries explicitly or
  map onto chat conversations with a status field.

**6. Saved payment methods** — `GET /payments/methods`
- UI: `payments.ts`, checkout. Shape: provider, masked number, `primary`.
- You store a `PaymentMethod` *string on a transaction*, but no vault of saved
  cards/momo per user. Add a payment-methods resource (or wire Paystack
  authorization tokens).

### C. Brand-new modules (no backend at all)

**7. Pricing settings** — `GET/PUT /pricing`
- `PricingSettings`: baseRate, weekendRate, weeklyDiscount%, monthlyDiscount%,
  minNights, cleaningFee. Per-listing. New controller + persistence.

**8. Owner Exchange (community forum)** — `GET /exchange/posts` (+ create/reply)
- `ExchangePost`: author, role, title, body, category
  (`Tips|Suppliers|Regulation|Marketplace|General`), replies, pinned, createdAt.
  Full CRUD + replies. New module.

**9. Host Tasks board** — `GET /tasks` (+ CRUD)
- `HostTask`: title, property, type (`cleaning|maintenance|inspection|restock`),
  priority, status (`todo|in-progress|done`), dueDate, assignee.
- Distinct from `/api/maintenance` (tenant tickets). New module.

**10. Team Users** — `GET /users` (+ invite/suspend)
- `TeamUser`: name, email, role (`owner|co-host|cleaner|maintenance|agent`),
  status (`active|invited|suspended`), properties count, lastActive.
- Per-landlord staff roster with invitations. New module.

**11. Resources** — `GET /resources`
- `Resource`: title, description, category (`guide|policy|template|video`),
  format, url. Likely a curated/admin-managed list. New (small) module.

**12. Statements** — `GET /statements`
- `Statement`: month, period, grossRevenue, managementFee, netPayout,
  status (`paid|pending`). Monthly payout statements (downloadable).
- You have live `earnings`; statements are the *periodised, downloadable*
  records. New module (can build on earnings data).

**13. Property virtual tour (hotspots)** — `GET /properties/{id}/tour`
- `PropertyTour`: rooms with name/area/caption/dimensions/media + clickable
  `hotspots` (x/y %, label, category, detail).
- **Decision needed:** this is a *different concept* from your
  **walkthrough video** (`POST /properties/{id}/walkthrough` + review). Pick one
  product direction — either feed the hotspot tour from walkthrough media, or
  drop the hotspot UI in favour of the video.

---

## Suggested build order

1. **Tiny/partial first** (1, 2, 3, 4) — unblocks the home page, calendar and
   landlord views with minimal work.
2. **Pricing settings (7)** + **statements (12)** — tied to the money flow you
   already have (earnings/escrow).
3. **Tasks (9)** + **Team users (10)** — landlord operations.
4. **Owner Exchange (8)** + **Resources (11)** — community/content, lowest risk
   to defer.
5. **Resolve the tour vs. walkthrough decision (13)** before either side builds
   more tour UI.
6. **Inquiries (5)** + **saved payment methods (6)** — finish the booking funnel.
