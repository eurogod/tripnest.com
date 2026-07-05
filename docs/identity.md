# TripNest — App Identity (derived from the backend)

This document defines what the TripNest app **is** and how the frontend should **look and behave**,
derived directly from what `TripNest.Core` actually implements. Each section reads the backend
structure (models, services, endpoints) and turns it into product identity + UI direction.

---

## 0. Core identity (what the backend makes this app)

The backend isn't a generic rentals CRUD. Three modules dominate it — **identity verification**
(Ghana Card via the TripNest.Id authority + face match), **escrow payments** (Paystack hold →
release), and **safety** (check-in, trusted contact, emergency). That structure means:

> **TripNest is a _trust-first_ rentals marketplace for Ghana.** It looks like Airbnb, but its
> defining promise is "every host, home, payment and arrival is verified and protected." Trust is
> not a feature page — it is the product's identity and must show on every surface.

Design consequence: a **calm, credible, teal** identity (carried from the verification ID card),
Airbnb-grade layout and photography, with **verification, escrow and safety signposted everywhere**
a person, listing, or payment appears.

---

## 1. People → audiences & navigation (`User.Role`, auth)

The backend has roles `Tenant, Landlord, Agent, Caretaker, Admin, Guest` and JWT + refresh auth.
→ The app is **multi-persona with context switching** (Airbnb's "switch to hosting" pattern):

- **Tenant/Guest** — the default explore/book experience.
- **Landlord** — a hosting context (list, manage, earnings).
- **Agent / Caretaker** — a service console.
- **Admin** — an ops console.

`IsVerified` gating (`RequireVerifiedAttribute` blocks Landlord/Agent/Caretaker actions) →
the UI must **gate host/service actions behind a visible verification step**, never fail silently.

## 2. Trust & identity → the signature of the whole app

Backend distinguishes **two independent things** — keep them visually distinct:
- `IsVerified` + `TripNestId` = **Ghana Card identity** (async: NIA lookup + face-match sidecar).
- `EmailVerified` / `PhoneVerified` = **contact ownership** (OTP, 60s cooldown → 429, 5/min limit).

→ Identity surfaces:
- A **Verified shield (teal)** wherever a host/renter/listing is shown — this is the brand's
  signature mark.
- A **Verification Center** with three independent tracks (Email · Phone · Ghana Card identity),
  each showing its own status; the identity track shows an **async "Verifying…" state** the user
  can leave and return to.
- The **TripNest ID card** (`TN-GH-YYYY-NNNNNN`, server-rendered PDF + QR) presented as a reward
  and identity artifact, not buried in settings.
- A **Trust score** chip (0–100) on profiles/listings.

## 3. Properties → listings & the host wizard (`Property`, walkthrough gate)

Property fields (`Title, Location, Latitude/Longitude, Bedrooms, Bathrooms, MonthlyRent, DailyRate,
PropertyType, StayType, Amenities, Photos, CancellationPolicy`) and a **walkthrough-video approval
gate** (`WalkthroughStatus`; a listing can't go `Active` until Approved).

→ UI:
- **Listing cards & detail** identical in structure to Airbnb (photo, rating, price). `StayType` +
  `DailyRate`/`MonthlyRent` drive **"per night" vs "per month"** pricing.
- **Map = Leaflet/OpenStreetMap** (backend stores lat/long), price-pin markers synced to cards.
- **Create-listing wizard** must make the **walkthrough step explicit**: upload video → "Under
  review" → listing stays Draft with a clear banner until approved.

## 4. Bookings + escrow → the booking flow is gated and protected

`Booking` (dates, amount, status) + availability + `CancellationPolicy`, and **escrow** over
**Paystack** (`initiate → hold → release / dispute / refund`).

→ The reserve flow is a **trust-gated stepper**, which is the app's most important screen:
`① Dates & price → ② Be verified → ③ Pay (escrow) → ④ Sign agreement → ✓ Booked`.
- Payment redirects to Paystack and returns to a result; funds show as **"Held in escrow — released
  after check-in,"** never "paid to host." This reassurance is core identity, not a footnote.
- Host dashboard exposes escrow state per booking: **Held / Release / Disputed / Refunded**.

## 5. Agreements & receipts → documents are real PDFs

`AgreementService` (create, **tenant + landlord signatures**, PDF, expiry) and `ReceiptService`
(PDF) now render real branded PDFs (QuestPDF).

→ UI: an **Agreement** step (sign in-app, download PDF) and a **Receipt** download on every booking.
Documents use the same teal identity as the ID card — they're brand touchpoints.

## 6. Communications → messaging, notifications, channels

`NotificationService` (in-app + SMS via TextBee + email via SMTP; `CommunicationPreference`
opt-out), real-time **chat over SignalR `/hubs/chat`**. WhatsApp was removed.

→ UI: an **Inbox** (real-time threads with property context), a **notification bell**, and
**Settings → channels** (SMS/email toggles). State clearly that **emergency safety alerts always
send** regardless of the toggles.

## 7. Safety → a first-class, consent-driven layer

`SafetyController`: saved **trusted contact** (`GET/PUT /api/safety/contact`, overridable per
request), **safe-arrival check-in** (notifies the contact; **location only with explicit per-
check-in consent**), and an **emergency alert** that bypasses opt-out.

→ UI: a **"Check in"** action on active bookings (asks "Share your location?" before anything), a
**trusted-contact** setting, and a distinct **SOS / Emergency** action in danger-red. Consent is a
visible, deliberate choice — this matches the server's consent gate and is part of the brand's
"safe" identity.

## 8. Services → agents, caretakers, maintenance

Agent **viewing requests**, caretaker **service requests/assignments + reviews**, tenant
**maintenance reports** (convertible to a service request).

→ A focused **service console** per role, plus tenant-side **"Report an issue"** with photos and a
visible status chain (Reported → Assigned → Resolved).

## 9. Reviews & trust score → social proof

`ReviewService` (property + user reviews, ratings) and the trust-score service.

→ Reviews use Airbnb's **category-chip breakdown**; add a **Safety** category to reflect the app's
identity. Trust score is surfaced as a chip with an explainer of what raises it.

---

## 10. Resulting design identity (the look)

Derived from the above, the visual identity is:

- **Brand color:** TripNest **teal** `#0F766E` (from the verification ID card) — trust/credibility,
  deliberately not Airbnb pink. Tint `#E7F6F3`, dark `#0B5751`.
- **Type:** Plus Jakarta Sans; large, confident headings; generous whitespace.
- **Shape & depth:** 14–16px rounded image cards, soft shadows, pill primary buttons, an elevated
  sticky booking widget.
- **Imagery-forward:** big property photography, edge-to-edge cards (Airbnb-grade).
- **The Verified shield** is the recurring identity mark; **escrow** and **safety** cues recur as
  reassurance microcopy ("Held in escrow", "you won't be charged yet", "Share location? Not now").
- **Voice:** calm, plain, reassuring — this is money + identity + safety in one product.

**Reference implementation:** `docs/mockup.html` (open in a browser) / `docs/mockup.png`, with the
full screen-by-screen spec in `docs/FRONTEND_UIUX.md`.

---

### One-line identity
> Airbnb's experience, rebuilt on a spine of verified identity, escrow-protected money, and
> consented safety — for Ghana. Teal, calm, and trustworthy by default.
