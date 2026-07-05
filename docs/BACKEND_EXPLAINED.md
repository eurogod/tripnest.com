# TripNest — Backend Guide (for everyone)

This document explains, in plain language, **everything the TripNest backend does, what it
accepts, and what it gives back** — written so that engineers, the frontend team, designers,
QA, and non-technical readers can all follow it.

If you only read one section, read **[3. Talking to the API](#3-talking-to-the-api)** and the
**[Frontend cheat-sheet](#19-frontend-cheat-sheet)** at the end.

> For the terse route-by-route reference (just methods + paths + access), see
> [`API.md`](../API.md). This guide is the friendly, explanatory companion.

---

## Table of contents

1. [What TripNest is](#1-what-tripnest-is)
2. [How the system is shaped](#2-how-the-system-is-shaped)
3. [Talking to the API](#3-talking-to-the-api)
4. [Accounts, login & sessions](#4-accounts-login--sessions)
5. [Roles — who can do what](#5-roles--who-can-do-what)
6. [The two kinds of "verified"](#6-the-two-kinds-of-verified)
7. [Identity verification (Ghana Card)](#7-identity-verification-ghana-card)
8. [Properties & listings](#8-properties--listings)
9. [Availability & the booking calendar](#9-availability--the-booking-calendar)
10. [Walkthroughs (video tours)](#10-walkthroughs-video-tours)
11. [Bookings & cancellations](#11-bookings--cancellations)
12. [Escrow & payments](#12-escrow--payments)
13. [Agreements & receipts](#13-agreements--receipts)
14. [Reviews & trust score](#14-reviews--trust-score)
15. [People services: caretakers, agents, maintenance](#15-people-services-caretakers-agents-maintenance)
16. [Chat & real-time messaging](#16-chat--real-time-messaging)
17. [Notifications, preferences & safety](#17-notifications-preferences--safety)
18. [Profile, settings, search, dashboards, config](#18-profile-settings-search-dashboards-config)
19. [Frontend cheat-sheet](#19-frontend-cheat-sheet)
20. [Enum reference (the numbers)](#20-enum-reference-the-numbers)
21. [Running it locally](#21-running-it-locally)

---

## 1. What TripNest is

TripNest is an **accommodation-booking platform built for Ghana**, with three ideas at its core:

- **Trust** — people are identity-verified against the national Ghana Card, and everyone carries
  a visible **trust score**.
- **Safety** — renters can register a trusted contact and "check in" on arrival; emergency
  alerts always get through.
- **Protected money** — payments are held in **escrow** (via Paystack) and only released when
  both sides are satisfied, with tiered refunds on cancellation.

Around that sit the usual building blocks: listings, a booking calendar, in-app chat, reviews,
rental agreements (as PDFs), receipts, maintenance requests, and connections to **agents**
(who show properties) and **caretakers** (who service them).

The backend is an **ASP.NET Core 8 Web API** backed by **PostgreSQL**. The frontend (and any
other client) talks to it entirely over **HTTPS + JSON**.

---

## 2. How the system is shaped

There are three running services. The frontend only ever talks to **Core**.

| Service | Local URL | What it is |
|---|---|---|
| **TripNest.Core** (this backend) | `http://localhost:5091` | The main API — everything in this doc |
| **TripNest.Id** | `http://localhost:5135` | A separate Ghana Card registry, used only during identity verification |
| **Face-match sidecar** (Python) | `http://localhost:5001` | Compares a selfie to the card photo and checks it's a live person |

```
                ┌─────────────────────┐
  Frontend ───► │   TripNest.Core API │ ───► PostgreSQL
  (web/app)     │   (this backend)    │
                └─────────┬───────────┘
                          │  (only during identity verification)
              ┌───────────┴────────────┐
              ▼                        ▼
        TripNest.Id            Face-match sidecar
      (Ghana Card data)       (selfie vs card + liveness)
```

Core runs fine on its own; the two sidecars are **only** needed for the identity-verification
flow. The backend also reaches out to three **external services**, all of which *degrade
gracefully* (if a key is missing it logs and no-ops instead of crashing):

- **TextBee** — sends SMS.
- **Gmail SMTP** — sends email.
- **Paystack** — takes escrow payments.

---

## 3. Talking to the API

### Base URL
- Local: `http://localhost:5091`
- Interactive API explorer (dev only): `http://localhost:5091/swagger`

### Every response has the same envelope
No matter the endpoint, success or failure, the body looks like this:

```json
{
  "message": "human-readable summary",
  "statusCode": 200,
  "data": { ... } | [ ... ] | null,
  "success": true
}
```

- `data` holds the actual payload (an object, a list, or `null`).
- `success` is just `statusCode` being in the 200–299 range — handy for the frontend.
- On errors, `message` explains what went wrong and `data` is usually `null`.

### Status codes you'll see
| Code | Meaning in TripNest |
|---|---|
| `200` | OK |
| `201` | Created (a new thing was made) |
| `400` | Bad request — validation failed, or an invalid operation |
| `401` | Not logged in / token missing or expired |
| `403` | Logged in, but not allowed (wrong role, **or identity not verified**) |
| `404` | Not found |
| `409` | Conflict (e.g. email already registered, dates already booked) |
| `424` | A dependency failed (e.g. a sidecar/payment service unreachable) |
| `429` | Too many requests — you hit a rate limit or a cooldown |

> **Errors are uniform.** The backend turns internal exceptions into these codes centrally, so
> the frontend can rely on `statusCode` + `message` everywhere instead of special-casing each
> endpoint.

### Two gotchas for the frontend
1. **Enums are sent as numbers, not strings.** A role of `Landlord` arrives as `1`, a booking
   `Confirmed` as `1`, etc. See [the enum reference](#20-enum-reference-the-numbers) for every
   mapping. Keep matching constants on the client.
2. **Dates are ISO-8601 UTC.** Send and expect strings like `2026-06-26T14:30:00Z`. Date-only
   fields (e.g. date of birth) are `YYYY-MM-DD`.

### Auth header
Protected endpoints need a bearer token (see next section):
```
Authorization: Bearer <accessToken>
```

---

## 4. Accounts, login & sessions

### Registering — `POST /api/auth/register` 🌐
Body:
```json
{
  "fullName": "Ama Mensah",
  "email": "ama@example.com",
  "password": "Str0ng@Pass",
  "confirmPassword": "Str0ng@Pass",
  "phone": "+233201234567",
  "role": 1
}
```
- `role` is the numeric `UserRole` (see [enums](#20-enum-reference-the-numbers)). **Admin cannot
  self-register.**
- The **phone number is validated offline** and normalised to international (E.164) format.
  Bad numbers are rejected with `400`.

### Logging in — `POST /api/auth/login` 🌐
Body: `{ "email": "...", "password": "..." }`. Returns:
```json
{
  "userId": "…",
  "fullName": "Ama Mensah",
  "email": "ama@example.com",
  "role": 1,
  "accessToken": "<JWT>",
  "refreshToken": "<token>",
  "isVerified": false,
  "emailVerified": false,
  "phoneVerified": false,
  "tripNestId": null
}
```

### How sessions work (JWT + refresh)
- The **access token** is a short-lived JWT. Put it in the `Authorization` header on every
  protected call.
- The **refresh token** is long-lived (stored hashed on the server). When the access token
  expires, call `POST /api/auth/refresh-token` with the refresh token to get a fresh pair —
  **without** forcing the user to log in again.
- `POST /api/auth/logout` revokes the refresh token. `POST /api/auth/change-password` also
  revokes it (so other sessions are kicked out).

### Other auth endpoints
| Endpoint | Purpose |
|---|---|
| `GET /api/auth/me` 🔒 | Who am I? (id, role, verification flags, TripNestId) |
| `POST /api/auth/forgot-password` 🌐 | Start a password reset |
| `POST /api/auth/reset-password` 🌐 | Finish a password reset |
| `POST /api/auth/change-password` 🔒 | Change while logged in |

### Verifying contact details (email & phone)
Separate, optional, and independent of each other:
- `POST /api/auth/email/send-otp` 🔒 → emails a 6-digit code; `POST /api/auth/email/verify-otp`
  with `{ "code": "123456" }` sets `emailVerified`.
- `POST /api/auth/phone/send-otp` 🔒 → texts a code; `.../phone/verify-otp` sets `phoneVerified`.
- Codes are **single-use, hashed, expire in 10 minutes, allow 5 attempts**, and there's a
  **60-second resend cooldown** plus a 5/min rate limit (both surface as `429`).

---

## 5. Roles — who can do what

Roles: **Tenant, Landlord, Agent, Caretaker, Admin, Guest.**

| Role | In short | Identity verification |
|---|---|---|
| **Guest / Tenant** | Browses and books accommodation | Optional — never blocked |
| **Landlord** | Lists properties, manages bookings/earnings | **Required** to list/manage |
| **Agent** | Shows properties, reviews video walkthroughs | **Required** for those actions |
| **Caretaker** | Services properties (repairs, upkeep) | **Required** to accept work |
| **Admin** | Platform oversight, dispute resolution | Seeded only; can't self-register |

**The key rule for the frontend:** Landlord/Agent/Caretaker can log in, view dashboards, edit
their profile, and complete verification — but their *core actions* (e.g. creating a listing,
accepting a job) return **`403`** until their identity is verified. Endpoints with this gate are
marked 🛡️ throughout. Design for "verify first" prompts on those flows.

---

## 6. The two kinds of "verified"

Don't confuse these — they're independent:

| Flag | What it proves | How it's set |
|---|---|---|
| `isVerified` | **Identity** — this is a real person with a valid Ghana Card | The async Ghana Card flow (next section) |
| `emailVerified` / `phoneVerified` | **Ownership** of that email / phone | The OTP flows above |

A user can have a verified phone but unverified identity, or vice-versa. Only `isVerified`
unlocks the 🛡️ actions and mints a public **TripNestId** badge.

---

## 7. Identity verification (Ghana Card)

This is the trust backbone. It's **asynchronous** — the user submits, the work happens in the
background, and the client **polls** for the result.

**Step 1 — submit:** `POST /api/verification/start` 🔒
```json
{
  "ghanaCardNumber": "GHA-123456789-0",
  "selfiePhotoPath": "uploads/selfies/abc.jpg",
  "firstName": "Ama",
  "lastName": "Mensah",
  "dateOfBirth": "1996-04-12"
}
```
Returns immediately with status **`Pending` (1)** so the UI can move on.

**Step 2 — the backend (in the background):**
1. Looks up the card against **TripNest.Id**.
2. Sends the selfie + the card photo to the **face-match sidecar**, which returns:
   - a **similarity score** (does the selfie match the card photo?), and
   - a **liveness score** (is this a live person, not a printed photo or a screen?).
3. Decides **Verified** or **Rejected**. Both the match *and* liveness must pass.

**Step 3 — poll:** `GET /api/verification/status` 🔒
```json
{
  "verificationId": "…",
  "ghanaCardNumber": "***-***6789-0",
  "status": 2,
  "faceMatchScore": 91.4,
  "livenessScore": 88.0,
  "failureReason": null,
  "submittedAt": "2026-06-26T10:00:00Z",
  "reviewedAt": "2026-06-26T10:00:07Z"
}
```
- `status`: `NotStarted=0, Pending=1, Verified=2, Rejected=3`.
- On **Rejected**, `failureReason` says why (e.g. *"Liveness check failed — please retake a live
  selfie…"*), a notification is posted, and the user may call `/start` again.
- The card number is **masked** in responses; the frontend should never display it in full.

> Liveness / anti-spoofing is **Phase 1 (passive)**: it inspects the single selfie to block the
> most common attack — holding up a photo of the cardholder. It does **not** yet prove the selfie
> is *fresh* (that's a planned Phase 2).

---

## 8. Properties & listings

What renters browse and landlords manage.

| Endpoint | Access | What it does |
|---|---|---|
| `GET /api/properties` | 🌐 | All **active** listings |
| `GET /api/properties/{id}` | 🌐 | One listing |
| `GET /api/properties/search?location=&minBedrooms=&maxBedrooms=` | 🌐 | Filtered search |
| `GET /api/properties/user/my-properties` | 🔒 | The caller's own listings |
| `POST /api/properties` | 🔒 🛡️ | Create a listing |
| `PUT /api/properties/{id}` | 🔒 🛡️ | Edit |
| `DELETE /api/properties/{id}` | 🔒 🛡️ | Remove |
| `POST /api/properties/{id}/photos` | 🔒 🛡️ | Upload photos (multipart, owner only) |

**Create body:**
```json
{
  "title": "Cozy 2-bed in East Legon",
  "description": "…",
  "location": "East Legon, Accra",
  "latitude": 5.6312, "longitude": -0.1660,
  "bedrooms": 2, "bathrooms": 2,
  "monthlyRent": 4500, "dailyRate": 250,
  "propertyType": "Apartment",
  "stayType": 0,
  "cancellationPolicy": 1,
  "amenities": "WiFi,Parking,Water"
}
```

**A listing comes back as** (`PropertyResponse`): `propertyId`, `title`, `description`,
`location`, `latitude`, `longitude`, `bedrooms`, `bathrooms`, `monthlyRent`, `dailyRate`,
`propertyType`, `stayType`, `cancellationPolicy`, `amenities`, `photoPaths`, `status`,
`createdAt`, `updatedAt`.

- `stayType`: `ShortTerm=0, LongTerm=1, Student=2`.
- `cancellationPolicy`: `Flexible=0, Moderate=1, Strict=2` (drives refunds — see
  [Bookings](#11-bookings--cancellations)).
- A new listing starts as **Draft**; only **Active** listings appear in public browse/search.

---

## 9. Availability & the booking calendar

Used to render a date-picker and prevent double-booking.

| Endpoint | Access | What it does |
|---|---|---|
| `GET /api/properties/{id}/availability` | 🌐 | Date ranges the landlord has **blocked** |
| `GET /api/properties/{id}/available-ranges?from=&to=` | 🌐 | **Open, bookable** ranges for the calendar |
| `POST /api/properties/{id}/blocked-dates` | 🔒 `[Landlord]` 🛡️ | Block a range |
| `DELETE /api/properties/{id}/blocked-dates/{blockedDateId}` | 🔒 `[Landlord]` 🛡️ | Unblock |

"Available" accounts for both landlord-blocked dates **and** existing confirmed bookings.

---

## 10. Walkthroughs (video tours)

Landlords upload a video walkthrough; an agent/admin reviews and approves it (a quality gate
before it's trusted).

| Endpoint | Access |
|---|---|
| `GET /api/properties/{id}/walkthroughs` · `/walkthroughs/{wId}` | 🌐 |
| `POST /api/properties/{id}/walkthrough` (multipart video) | 🔒 `[Landlord]` 🛡️ |
| `PATCH /api/properties/{id}/walkthrough/review` | 🔒 `[Agent,Admin]` 🛡️ |
| `GET /api/properties/pending-walkthroughs` | 🔒 `[Agent,Admin]` |
| `DELETE /api/properties/{id}/walkthroughs/{wId}` | 🔒 `[Landlord,Admin]` 🛡️ |

`WalkthroughStatus`: `NotSubmitted=0, PendingReview=1, Approved=2, Rejected=3`.

---

## 11. Bookings & cancellations

| Endpoint | Access | What it does |
|---|---|---|
| `POST /api/bookings` | 🔒 | Create a booking (checks availability first) |
| `GET /api/bookings/{id}` | 🔒 | The tenant or that property's landlord only |
| `GET /api/bookings/user/my-bookings` | 🔒 | The caller's bookings |
| `GET /api/bookings/{id}/cancellation-preview` | 🔒 | Refund % + amount, **no change made** |
| `POST /api/bookings/{id}/cancel` | 🔒 | Cancel (owner only), refund issued via the gateway |

**Create body:**
```json
{ "propertyId": "…", "checkInDate": "2026-07-01T14:00:00Z", "checkOutDate": "2026-07-05T11:00:00Z" }
```

`BookingStatus`: `Pending=0, Confirmed=1, CheckedIn=2, CheckedOut=3, Cancelled=4, Completed=5`.

**Cancellations are policy-tiered.** The refund depends on the property's `cancellationPolicy`
and how close to check-in you cancel. Always show the user
`GET /…/cancellation-preview` (refund %/amount, no side effects) **before** they confirm
`POST /…/cancel`.

---

## 12. Escrow & payments

Money is **held in escrow** (via Paystack) and only released when appropriate — the platform's
financial trust mechanism.

| Endpoint | Access | What it does |
|---|---|---|
| `POST /api/escrow/initiate` | 🔒 | Start a payment → returns a Paystack `checkoutUrl` + `paymentReference` |
| `POST /api/escrow/webhook` | 🌐 (Paystack-signed) | Paystack calls this when money lands; signature is verified |
| `GET /api/escrow/{id}` | 🔒 | Escrow status |
| `POST /api/escrow/{id}/release` | 🔒 | Release funds (deal completed happily) |
| `POST /api/escrow/{id}/dispute` | 🔒 | Raise a dispute |
| `PATCH /api/escrow/{id}/resolve-dispute` | 🔒 `[Admin]` | Admin resolves |
| `POST /api/escrow/{id}/refund` | 🔒 `[Admin]` | Admin refunds |

**The frontend payment flow:**
1. Call `initiate` → get `checkoutUrl`.
2. Redirect / open the Paystack checkout there.
3. Paystack notifies the backend via the **webhook** (server-to-server — not the browser). The
   webhook is HMAC-signature-verified; the charged amount must equal the booking total or the
   hold is rejected.
4. Poll `GET /api/escrow/{id}` to reflect the new status.

`EscrowStatus`: `Pending=0, HeldInEscrow=1, Released=2, Refunded=3, Disputed=4`.

> Without Paystack keys configured, escrow returns a **simulated** reference so the rest of the
> app still works in dev — the frontend flow is identical.

---

## 13. Agreements & receipts

Both are generated server-side as **PDFs**.

**Agreements** (rental contracts) — `api/agreements`:
| Endpoint | Access |
|---|---|
| `POST /` create · `GET /mine` · `GET /{id}` | 🔒 |
| `POST /{id}/sign` | 🔒 |
| `GET /{id}/download` (PDF) | 🔒 |

`AgreementStatus`: `Draft=0, Pending=1, Signed=2, Expired=3, Terminated=4`.

**Receipts** — `api/receipts`:
| Endpoint | Access |
|---|---|
| `GET /mine?page=&pageSize=` · `GET /{id}` · `GET /booking/{bookingId}` | 🔒 |
| `GET /{id}/download` (PDF) | 🔒 |

Download endpoints return a PDF file stream (not the JSON envelope) — fetch them as a blob.

---

## 14. Reviews & trust score

**Reviews** — `api/reviews`. People review properties, tenants, and landlords.
| Endpoint | Access |
|---|---|
| `GET /property/{id}?page=&pageSize=` · `GET /{id}` | 🌐 |
| `POST /` · `GET /mine` · `DELETE /{id}` | 🔒 |

`ReviewType`: `Property=0, Tenant=1, Landlord=2`.

**Trust score** — `api/trustscore`. A visible reputation number for users and properties.
| Endpoint | Access |
|---|---|
| `GET /property/{id}` · `GET /user/{id}` | 🌐 |
| `POST /stay-feedback` | 🔒 |

A background job snapshots scores over time, so trends (`Improving=0, Declining=1, Stable=2`)
can be shown.

---

## 15. People services: caretakers, agents, maintenance

**Caretakers** — `api/caretakers` (they service properties):
| Endpoint | Access |
|---|---|
| `GET /` · `GET /{id}` | 🌐 |
| `POST /assign` | 🔒 `[Landlord]` 🛡️ |
| `POST /service-requests` · `GET /service-requests/mine` | 🔒 |
| `PATCH /service-requests/{id}/accept` | 🔒 `[Caretaker]` 🛡️ |
| `PATCH /service-requests/{id}/status` · `POST /service-requests/{id}/review` | 🔒 |

`ServiceRequestStatus`: `Pending=0, Accepted=1, InProgress=2, Completed=3, Cancelled=4`.

**Agents** — `api/agents` (they show properties to prospective tenants):
| Endpoint | Access |
|---|---|
| `GET /` · `GET /{id}` | 🌐 |
| `POST /{id}/viewing-requests` | 🔒 `[Tenant]` |
| `PATCH /viewing-requests/{id}/status` | 🔒 `[Agent,Tenant]` 🛡️ |

`ViewingRequestStatus`: `Pending=0, Confirmed=1, Cancelled=2, Completed=3`.

**Maintenance** — `api/maintenance` (tenants report issues):
| Endpoint | Access |
|---|---|
| `POST /` (report) · `PATCH /{id}/status` | 🔒 |
| `GET /property/{id}` | 🔒 `[Landlord,Admin]` |
| `GET /mine` | 🔒 `[Tenant]` |
| `POST /{id}/convert-to-service-request` | 🔒 `[Landlord,Admin]` 🛡️ |

`MaintenanceStatus`: `Reported=0, Assigned=1, InProgress=2, Completed=3, Cancelled=4`. A reported
issue can be converted into a caretaker service request.

---

## 16. Chat & real-time messaging

There are **two ways** to use chat, and they stay in sync:

**A) REST** — `api/chat` (works without websockets):
| Endpoint | Access |
|---|---|
| `GET /conversations/mine` | 🔒 |
| `POST /conversations` · `GET /conversations/{id}` | 🔒 |
| `GET /conversations/{id}/messages?page=&pageSize=` | 🔒 |
| `POST /conversations/{id}/messages` | 🔒 |
| `PATCH /messages/{id}/read` · `PATCH /conversations/{id}/mark-read` | 🔒 |
| `DELETE /conversations/{id}` | 🔒 |

`MessageType`: `Text=0, Image=1, File=2, Document=3`.

**B) Real-time (SignalR)** — hub at **`/hubs/chat`** (needs JWT):
- Browser clients pass the token on the websocket handshake as a query string:
  `/hubs/chat?access_token=<JWT>`.
- **Server → client events:** `ReceiveMessage`, `UserTyping`, `UserStoppedTyping`.
- **Client → server methods:** `Typing`, `StopTyping`.

> Sending a message via the REST `POST …/messages` **also broadcasts it live** over SignalR, so a
> client that isn't connected to the hub still works, and connected clients see it instantly.

---

## 17. Notifications, preferences & safety

**Notifications** — `api/notifications` (the in-app bell):
| Endpoint | Access |
|---|---|
| `GET /mine?page=&pageSize=` · `GET /unread-count` | 🔒 |
| `PATCH /{id}/read` · `PATCH /mark-all-read` · `DELETE /{id}` | 🔒 |

`NotificationType`: `BookingConfirmed=0, PaymentReceived=1, AgreementReady=2,
MaintenanceUpdate=3, ServiceRequestUpdate=4, SafetyAlert=5, VerificationStatusChanged=6,
General=7`. Every notification is recorded **in-app**, and *then* optionally pushed via SMS/email
based on the user's preferences.

**Communication preferences** — `api/communication-preferences`:
| Endpoint | Access |
|---|---|
| `GET /mine` · `PUT /mine` (`{ smsEnabled, emailEnabled }`) | 🔒 |

Opt-outs for SMS and email are independent. **Emergency safety alerts always send, regardless of
opt-out.**

**Safety** — `api/safety`:
| Endpoint | Access | What it does |
|---|---|---|
| `GET /contact` · `PUT /contact` (`{ name, phone, email }`) | 🔒 | Your saved trusted contact |
| `POST /checkin` | 🔒 | "I've arrived safely" → notifies the contact |
| `POST /alert` (`{ bookingId }`) | 🔒 | Emergency alert |

The check-in body is
`{ bookingId, contactPhone?, contactEmail?, shareLocation, latitude?, longitude? }` — **location
is only attached when the user explicitly consents** (`shareLocation: true`).

---

## 18. Profile, settings, search, dashboards, config

**Profile** — `api/profile`: `GET /me`, `PUT /me`, `POST /photo` (multipart). Note: there's also
a verified-member **ID card PDF** at `GET /api/profile/id-card`.

**Settings** — `api/settings`: `GET/PUT /notifications` (wraps communication preferences),
`PUT /password`, `DELETE /account`.

**Search** — `GET /api/search?q=&type=` 🌐: a general search across types.

**Config** — `GET /api/config/app-info` 🌐: client bootstrap config (e.g. map tile settings).
Good first call for the app to fetch on load.

**Dashboards** (role-specific roll-ups):
| Endpoint | Role |
|---|---|
| `GET /api/personaldashboard/tenant\|landlord\|agent\|caretaker` | matching role |
| `GET /api/landlord/stats` · `/earnings` · `/properties/performance` | `[Landlord]` |
| `GET /api/admin/stats` · `/audit-logs?userId=&limit=` | `[Admin]` |

---

## 19. Frontend cheat-sheet

**Auth**
- Log in → store `accessToken` + `refreshToken`.
- Send `Authorization: Bearer <accessToken>` on every 🔒 call.
- On `401`, call `POST /api/auth/refresh-token`; if that also fails, send the user to log in.

**Reading responses**
- Always unwrap `data` from the envelope; trust `success` / `statusCode`.
- Treat **all enums as numbers** — keep the [tables below](#20-enum-reference-the-numbers) as
  constants on the client.
- Dates are ISO-8601 UTC strings.

**Verification gating (UX)**
- Landlord/Agent/Caretaker: expect `403` on 🛡️ actions until `isVerified` is true. Show a
  "verify your identity" prompt rather than a raw error.

**Async patterns**
- Identity verification and escrow are **poll-based**: kick off, then poll `…/status` /
  `escrow/{id}`.
- Chat is **push-based**: connect to `/hubs/chat?access_token=…` for live messages/typing; fall
  back to REST if not connected.

**Money**
- For cancellations, show `cancellation-preview` before `cancel`.
- For payments, redirect to Paystack `checkoutUrl`; the result arrives via the server webhook —
  poll the escrow to reflect it.

**Lists**
- Paginated endpoints take `?page=&pageSize=`.

**Uploads**
- Photos, walkthrough videos, and profile pictures use **`multipart/form-data`**, not JSON.

**Rate limits**
- Global ~100 requests/min; OTP sends 5/min with a 60s resend cooldown. Over-limit → `429`;
  back off and surface a friendly "try again shortly".

**Health**
- `GET /health/live` (is it up), `GET /health/ready` (is it ready to serve).

---

## 20. Enum reference (the numbers)

The backend serializes enums as integers. These are the mappings (0-based, in declaration order):

| Enum | Values |
|---|---|
| **UserRole** | Tenant=0, Landlord=1, Agent=2, Caretaker=3, Admin=4, Guest=5 |
| **VerificationStatus** | NotStarted=0, Pending=1, Verified=2, Rejected=3 |
| **PropertyStatus** | Draft=0, Active=1, Inactive=2, Archived=3 (only Active is publicly visible) |
| **StayType** | ShortTerm=0, LongTerm=1, Student=2 |
| **CancellationPolicy** | Flexible=0, Moderate=1, Strict=2 |
| **WalkthroughStatus** | NotSubmitted=0, PendingReview=1, Approved=2, Rejected=3 |
| **BookingStatus** | Pending=0, Confirmed=1, CheckedIn=2, CheckedOut=3, Cancelled=4, Completed=5 |
| **EscrowStatus** | Pending=0, HeldInEscrow=1, Released=2, Refunded=3, Disputed=4 |
| **AgreementStatus** | Draft=0, Pending=1, Signed=2, Expired=3, Terminated=4 |
| **ReviewType** | Property=0, Tenant=1, Landlord=2 |
| **RentFrequency** | Nightly=0, Monthly=1, PerSemester=2 |
| **TrustScoreTrend** | Improving=0, Declining=1, Stable=2 |
| **ServiceRequestStatus** | Pending=0, Accepted=1, InProgress=2, Completed=3, Cancelled=4 |
| **ViewingRequestStatus** | Pending=0, Confirmed=1, Cancelled=2, Completed=3 |
| **MaintenanceStatus** | Reported=0, Assigned=1, InProgress=2, Completed=3, Cancelled=4 |
| **AgentStatus** / **CaretakerStatus** | Active=0, Inactive=1, Suspended=2 |
| **MessageType** | Text=0, Image=1, File=2, Document=3 |
| **NotificationType** | BookingConfirmed=0, PaymentReceived=1, AgreementReady=2, MaintenanceUpdate=3, ServiceRequestUpdate=4, SafetyAlert=5, VerificationStatusChanged=6, General=7 |

---

## 21. Running it locally

**Demo accounts (Development only):**

| Role | Email | Password |
|---|---|---|
| Admin | `admin@tripnest.local` | `Admin@123456` |
| Landlord | `kwame@tripnest.local` | `Landlord@123456` |
| Tenant | `kofi@tripnest.local` | `Tenant@123456` |
| Agent | `ekow@tripnest.local` | `Agent@123456` |
| Caretaker | `ebo@tripnest.local` | `Caretaker@123456` |

(More seeded accounts exist — see [`API.md`](../API.md).)

**Quick smoke test:**
1. `GET /api/config/app-info` — no auth, confirms the API is up.
2. `POST /api/auth/login` with a demo account — get a token.
3. `GET /api/properties` — see seeded listings.
4. `GET /api/auth/me` with the token — confirm your role and verification flags.

**Notes**
- Swagger (`/swagger`) is the fastest way to explore live in dev.
- The two verification sidecars and the SMS/email/Paystack integrations are all optional for
  general development — the app runs and degrades gracefully without them.

---

*This guide is a plain-language companion to [`API.md`](../API.md). If a route or behavior changes,
update both.*
