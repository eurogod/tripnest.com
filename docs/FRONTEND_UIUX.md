# TripNest — Frontend UI/UX Design

Airbnb-inspired rentals marketplace for Ghana, but **trust-first**: Ghana Card identity
verification, escrow payments, signed agreements and safe-arrival check-in are first-class,
not afterthoughts. This spec maps the UI directly to what `TripNest.Core` already exposes.

> Reference feel: Airbnb (clean cards, big imagery, split map+list search, a sticky booking
> widget). Brand: TripNest teal (carried from the PDFs/ID card), warmer and more "verified"
> than Airbnb's pink.

---

## 1. Brand & visual language

**Color tokens**
```
--brand:        #0F766E   teal            (primary actions, links, active)
--brand-dark:   #115E59   hovers, headings
--brand-tint:   #D1FAE5   selected/info backgrounds, verified chips
--ink:          #111827   primary text
--muted:        #6B7280   secondary text
--line:         #E5E7EB   borders/dividers
--surface:      #F9FAFB   page background / cards
--success:      #16A34A   verified, paid, released
--warning:      #D97706   pending, action-needed
--danger:       #DC2626   emergency/SOS, disputes, cancel
--gold:         #F59E0B   ratings/trust score
```
**Type:** Inter / system stack. Display 28–40 (hero), H1 24, H2 18, body 15–16, caption 12–13.
**Shape:** 12px card radius, 8px inputs, pill buttons for primary CTAs, soft shadow on cards
(`0 1px 3px rgba(0,0,0,.08)`), elevated shadow on the booking widget and modals.
**Imagery:** photography-forward (property photos fill cards edge-to-edge); generous whitespace.
**Tone:** calm and reassuring — this is money + identity in one product.

**The signature element — the Verified badge.** A teal shield + "Verified" appears on hosts,
listings and profiles whose owner passed Ghana Card verification (`IsVerified` + `TripNestId`).
This is TripNest's trust differentiator and should appear consistently everywhere a person or
listing is represented.

---

## 2. Design system (components)

- **Buttons:** Primary (teal pill), Secondary (outline), Ghost, Danger. Loading + disabled states.
- **Inputs:** text, password (with reveal), OTP (6 boxes), date range, number stepper (guests/
  bedrooms), file/photo upload (drag-drop), video upload (walkthrough), search combobox.
- **Cards:** ListingCard, BookingCard, MessageThreadCard, ReceiptRow, AgreementRow, ServiceRequestCard.
- **Chips/badges:** Verified ✓, status pills (Pending/Confirmed/Cancelled, Escrow Held/Released/
  Disputed, Walkthrough Pending/Approved), amenity chips, trust-score star.
- **Map:** Leaflet + OpenStreetMap (backend stores lat/long). Price-pin markers; hover syncs
  card ↔ pin; cluster on zoom-out.
- **Overlays:** right-side drawer (filters, notifications), center modal (auth, OTP, confirm),
  bottom sheet on mobile, toast for async outcomes (SMS/email "sent", payment result).
- **Feedback:** skeleton loaders on cards/map; empty states with a clear CTA; inline 429 cooldown
  ("Resend in 43s") on OTP screens.

---

## 3. Information architecture & navigation

**Global top bar (Airbnb-style):**
```
[ TripNest ▴ ]        ⌕ Where to? · Dates · Guests        [ Switch to hosting ] [ ✉ ] [ 🔔 ] [ 👤▾ ]
```
- Center pill search expands to a panel: **Location** (map/region), **Dates** (range), **Guests**,
  plus a **Stay type** toggle (Short stay / Long-term rent — backend `StayType`).
- Avatar menu is role-aware: Trips, Messages, Wishlist, **Verification**, **My TripNest ID**,
  Account/Settings, and (for hosts) Listings & Hosting dashboard.
- "Switch to hosting" flips Tenant ↔ Host context (Airbnb pattern).

**Mobile:** bottom tab bar — Explore · Wishlist · Trips · Inbox · Profile.

**Role surfaces:** Tenant (default), Host (Landlord), Agent/Caretaker (service dashboard),
Admin (ops console). The same account can hold multiple roles; context switch via the avatar menu.

---

## 4. Tenant journey (lead flow)

### 4.1 Home / Explore
```
┌──────────────────────────────────────────────────────────────────┐
│  [TripNest]      ⌕ Where to?  ·  Add dates  ·  Guests       👤▾   │
├──────────────────────────────────────────────────────────────────┤
│  Category strip:  🏠 Apartments  🏡 Houses  🛏 Rooms  🏙 Short stay │
│                   ⚲ Long-term   ⛱ Near me   ✓ Verified hosts        │
├──────────────────────────────────────────────────────────────────┤
│  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐                  │
│  │ [photo] │ │ [photo] │ │ [photo] │ │ [photo] │   … listing grid  │
│  │East Legon│ │Osu      │ │Kumasi   │ │Takoradi │                  │
│  │GHS1,200/mo│ │GHS350/nt│ │★4.8 ✓Verified│ … │                  │
│  └─────────┘ └─────────┘ └─────────┘ └─────────┘                  │
└──────────────────────────────────────────────────────────────────┘
```
ListingCard: photo carousel, location, price (monthly **or** nightly per `StayType`/`DailyRate`),
rating, ❤ wishlist, and a **Verified host** chip.

### 4.2 Search results — split map + list (the Airbnb signature)
```
┌──────────────── filters bar: Price · Beds · Type · Amenities · ✓Verified ─┐
│  LISTINGS (scroll)                    │            MAP (sticky)            │
│  ┌──────────┐ ┌──────────┐            │     ●GHS1.2k     ●GHS900           │
│  │ [photo]  │ │ [photo]  │            │           ●GHS1.5k                 │
│  │ ★4.9 ✓   │ │ ★4.7     │            │    ●GHS750   (hover card=pin)      │
│  └──────────┘ └──────────┘            │                                    │
└───────────────────────────────────────┴────────────────────────────────────┘
```
Filters map to property fields: price (`MonthlyRent`/`DailyRate`), `Bedrooms`/`Bathrooms`,
`PropertyType`, `Amenities`, `CancellationPolicy`, and a **Verified hosts only** toggle.

### 4.3 Property detail
```
┌── Gallery (1 big + 4 thumbs, "Show all photos") ─────────────────────────┐
│  Title · Location · ★4.8 (24 reviews) · ♡ Save · ⇪ Share                  │
├───────────────────────────────────────────┬──────────────────────────────┤
│  Host card: [avatar] Ama M.  ✓ Verified    │  ┌─ Booking widget (sticky) ─┐│
│             TripNest ID · Trust 92 · ★4.9   │  │ GHS1,200 / month          ││
│  ── 2 bed · 1 bath · Apartment · Short stay │  │ [ Check-in  Check-out ]   ││
│  Description …                              │  │ [ Guests ▾ ]              ││
│  Amenities: WiFi, Water, Parking, Security  │  │ Cancellation: Moderate    ││
│  Map (Leaflet/OSM) — approx pin             │  │ ───────────────────────   ││
│  Cancellation policy · House rules          │  │ [   Reserve   ]           ││
│  Reviews (rating breakdown + list)          │  │ Escrow-protected 🔒       ││
└─────────────────────────────────────────────┴───┴───────────────────────────┘
```
Host card surfaces **Verified badge + TripNest ID + Trust score** (`IsVerified`, `TripNestId`,
trust-score service). "Escrow-protected" reassurance sits under Reserve. **Contact host** opens
chat (SignalR). Reviews come from the reviews module with a rating breakdown.

### 4.4 Reserve → verify → pay (the critical path)
A stepper, because TripNest gates booking on a verified, contactable renter:

```
①  Confirm dates & price   →   ②  Be verified   →   ③  Pay (escrow)   →   ④  Agreement   →   ✓ Booked
```
- **② Verify gate.** If the renter isn't verified, this step blocks with a friendly explainer:
  - **Contact:** verify email and/or phone — 6-box OTP screen, resend with the **60s cooldown**
    shown as a countdown (`429` → "Resend in 43s"). Backend: `api/auth/email|phone/{send,verify}-otp`.
  - **Identity (for higher-trust actions):** Ghana Card flow — enter card number, name, DOB, take a
    **selfie** (camera), submit. Then a **"Verifying…" pending state** (the backend runs NIA + face
    match async via a queue) the user can leave and return to; on success they get the **Verified
    badge + a TripNest ID card** they can download as PDF.
- **③ Pay.** Reserve creates the booking; payment initializes **Paystack** and redirects to the
  Paystack checkout, returns to a result screen. Funds are shown as **held in escrow**, not yet
  released to the host — a labelled, reassuring state.
- **④ Agreement.** Generate the rental agreement, **sign** (tenant signature), download the PDF.
  Landlord counter-signs from their side.

### 4.5 Trips (tenant dashboard)
```
Tabs:  Upcoming │ Current │ Past │ Cancelled
┌─ BookingCard ───────────────────────────────────────────────┐
│ [photo] East Legon 2-bed · Jun 24–Jul 24 · Confirmed         │
│ Escrow: Held 🔒   [ Message host ] [ Agreement ] [ Receipt ] │
│ ── Safe arrival:  [ I've arrived — check in ]                │
└───────────────────────────────────────────────────────────────┘
```
Per booking: status, escrow state, **Agreement** (PDF) and **Receipt** (PDF) downloads, message
host, raise dispute, cancel (per cancellation policy), and **safe-arrival check-in** (§7).

---

## 5. Host (Landlord) journey

### 5.1 Create-listing wizard (with the walkthrough gate)
```
Step 1 Place & type   Step 2 Details (beds/baths/amenities)   Step 3 Photos
Step 4 Pricing (monthly / nightly · cancellation policy)
Step 5 Location (drop pin on Leaflet map → lat/long)
Step 6 Walkthrough video  ← REQUIRED. Upload → status: "Under review"
   ⤷ Listing stays DRAFT and cannot go live until walkthrough = Approved.
```
Make the **walkthrough approval gate** explicit: a banner on the draft listing —
"Pending walkthrough review. Your listing goes live once approved." (`WalkthroughStatus`).

### 5.2 Hosting dashboard
```
Today: 2 check-ins · 1 escrow ready to release · 3 unread messages
┌ Listings (status: Active / Draft / Pending review) ┐
┌ Reservations: Pending → Confirmed → Completed ─────┐
┌ Earnings: Escrow held vs Released ────────────────┐
```
- **Reservations:** confirm/decline, message guest, view agreement.
- **Earnings/Escrow:** per booking — Held / **Release funds** / Disputed / Refunded (escrow service).
  Releasing is a deliberate, confirmed action.
- **Reviews:** ratings received; respond.
- Host must be **Verified** to publish (RequireVerified) — surface the verification CTA prominently.

---

## 6. Agents, caretakers & maintenance (service console)

A focused dashboard for `Agent`/`Caretaker` roles:
- **Agents:** incoming **viewing requests** (accept/schedule/decline).
- **Caretakers:** **service requests** queue (accept, update status, complete), assignments to
  properties, and **service reviews** received.
- **Maintenance (tenant-initiated):** report an issue with photos → landlord triages → optionally
  **convert to a caretaker service request**. Show the chain status to all parties.

---

## 7. Trust, safety & identity (TripNest's differentiator)

**Verification center** (in profile): three independent tracks with clear status —
Email ✓ · Phone ✓ · **Ghana Card identity ✓** — each with its own CTA. Identity shows the async
"Verifying…" state and, on success, the **TripNest ID card**:
```
┌──────────────────────────────────────────┐
│  TRIPNEST                 VERIFIED MEMBER  │  ← teal band
│  [photo]  Ama Mensah                       │
│           Landlord                         │
│           TRIPNEST ID                      │
│           TN-GH-2026-000042      [▦ QR]    │
│           Member since Jun 2026            │
└──────────────────────────────────────────┘   [ Download PDF ]  [ Add to wallet ]
```
Mirrors the server-rendered card (`GET /api/profile/id-card`); QR scans to a verify view.

**Safe-arrival check-in** (from a booking):
```
[ I've arrived — check in ]
  → "Share your live location with your trusted contact?"  [ Not now ] [ Share location ]
  → Sends "arrived safely" (+ map link only if consented) to the saved trusted contact.
```
Consent is explicit and per check-in (matches the server's consent gate). A separate **SOS /
Emergency** action (danger-red) triggers the emergency alert that bypasses notification opt-out.
**Trusted contact** is managed in Settings (`GET/PUT /api/safety/contact`).

**Trust score** shows as a 0–100 chip on profiles/listings (trust-score service), with a tooltip
explaining what raises it (verification, reviews, completed stays).

---

## 8. Messaging & notifications

- **Inbox / chat:** thread list + conversation pane; real-time via the **SignalR `/hubs/chat`**;
  read receipts, typing, property context chip at the top of a thread. Mobile = full-screen thread.
- **Notifications:** bell → dropdown/drawer (booking updates, escrow, verification outcome, safety).
  **Settings → Notification preferences** toggles SMS/email channels (opt-out; emergency safety
  alerts always send — state this in the UI so toggling SMS/email doesn't imply silencing SOS).

---

## 9. Backend capability → UI surface (coverage map)

| Backend capability | Where it appears in the UI |
|---|---|
| Auth (JWT/refresh), register/login, forgot/reset | Auth modal, password reset screens |
| Email OTP / Phone OTP (cooldown→429) | OTP step in verify gate + Verification center |
| Ghana Card verification (async, NIA + face match) | Identity flow, "Verifying…" state, Verified badge |
| `TripNestId` + ID card PDF | Profile → My TripNest ID, download PDF |
| Trust score | Trust chip on profiles & listings |
| Properties (fields, amenities, photos, StayType) | Listing grid, detail, create-listing wizard |
| Leaflet/OSM lat/long | Search map, detail map, host pin-drop |
| Walkthrough approval gate | Wizard step 6 + draft-listing banner |
| Bookings + availability + cancellation policy | Booking widget, Trips, reservations |
| Paystack + escrow (hold/release/dispute/refund) | Pay step, Trips escrow state, host earnings |
| Agreements (create/sign/PDF) | Reserve step ④, Trips, host reservations |
| Receipts (PDF) | Trips → Receipt download |
| Notifications + preferences | Bell, Settings → notifications |
| Chat (SignalR) | Inbox, Contact host |
| Safe check-in + trusted contact + emergency | Booking check-in, SOS, Settings |
| Caretakers / agents / maintenance | Service console, maintenance reporting |
| Admin dashboard (verified counts, etc.) | Ops console |

---

## 10. Responsive & accessibility

- **Breakpoints:** ≥1280 split map+list (50/50 → 60/40); 768–1279 list with a "Map" toggle button;
  <768 full-screen map toggle, bottom-sheet filters, bottom tab nav.
- Sticky booking widget on desktop becomes a **fixed bottom price+Reserve bar** on mobile.
- WCAG AA contrast (teal on white passes); visible focus rings; OTP and date inputs keyboard-friendly;
  map has a list fallback; all status conveyed by text+icon, not color alone.

---

## 11. Recommended build stack (not prescriptive)

React + TypeScript, Vite, TanStack Query (the API is REST + `ApiResponse<T>` envelope), React Router,
Tailwind (tokens above), `react-leaflet` for the map, `@microsoft/signalr` for chat, and the JWT
access/refresh pattern the backend already implements. Components map 1:1 to §2.

---

### Suggested next steps
1. Build the **design tokens + core components** (§2) first — everything else composes from them.
2. Ship the **tenant critical path** (explore → detail → verify → pay → agreement) end-to-end.
3. Layer in host listing wizard, messaging, then the service console.
