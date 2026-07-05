# TripNest.Core — API Reference

.NET 8 Web API backend for an accommodation-booking platform centred on trust,
identity verification, and escrow-protected payments (Ghana-oriented).

- **Base URL (local):** `http://localhost:5091`
- **Interactive docs:** `http://localhost:5091/swagger` (Development only)
- **Health checks:** `GET /health/live` (liveness), `GET /health/ready` (readiness — Postgres gates, verification sidecars reported but non-gating), `GET /health` (full report).
- **Auth:** JWT Bearer. Obtain a token from `POST /api/auth/login`, then send
  `Authorization: Bearer <accessToken>` on protected routes.
- **Response envelope:** every endpoint returns
  `{ "message": string, "statusCode": int, "data": T | null, "success": bool }`.

## Companion services (sidecars)

| Service | URL | Purpose |
|---|---|---|
| **TripNest.Core** (this API) | `http://localhost:5091` | Main backend |
| **TripNest.Id** | `http://localhost:5135` | Ghana Card registry used during verification |
| **Face-match** (Python/DeepFace) | `http://localhost:5001` | Compares selfie ↔ card photo |

Core runs standalone; the two sidecars are only required for the identity-verification flow.

## External integrations (keys via user-secrets)

| Integration | Used for | Config keys |
|---|---|---|
| **TextBee** | SMS notifications (Android gateway relay) | `TextBeeSettings:{BaseUrl,ApiKey,DeviceId}` |
| **SMTP (Gmail)** | Email notifications | `SmtpSettings:{Host,Port,UseStartTls,Username,Password,FromEmail,FromName}` |
| **Paystack** | Escrow payments (test/live) | `PaystackSettings:{SecretKey,PublicKey,CallbackUrl}` |

All three channels (SMS, email, Paystack) **degrade gracefully** when unconfigured — they log
and no-op (SMS/email) or return a simulated reference (Paystack), so the app runs without
credentials. Set real keys with `dotnet user-secrets set "<key>" "<value>"`.

**Phone numbers** are validated offline (libphonenumber, default region `Phone:DefaultRegion`,
GH) at registration and normalised to E.164 — invalid numbers are rejected with 400.

**Contact verification** is independent for email and phone (use either or both): `POST
/api/auth/email/send-otp` / `POST /api/auth/phone/send-otp` send a single-use 6-digit code
(hashed, 10-min expiry, 5-attempt cap, 60s resend cooldown → 429, plus a 5/min rate limit), and
the matching `verify-otp` sets the user's `EmailVerified` / `PhoneVerified` flag. These are
separate from `IsVerified` (Ghana Card identity).
Notification opt-out covers SMS and email independently; emergency safety alerts ignore it.

## Roles

`Tenant`, `Landlord`, `Agent`, `Caretaker`, `Admin`, `Guest`.

- **Guest / unverified Tenant** — browse and book freely; verification optional.
- **Landlord / Agent / Caretaker** — identity verification is **compulsory**; their core
  actions return **403** until verified (marked 🛡️ below). They can still log in, view
  dashboards, edit their profile, and complete verification.
- **Admin** — cannot self-register (blocked at registration); seeded only.

## Identity verification flow (async)

1. `POST /api/verification/start` validates and queues the request, returning **`Pending`**
   immediately so the client can advance.
2. A background worker calls TripNest.Id (card lookup) and the face-match sidecar, then
   resolves the status to **`Verified`** or **`Rejected`**.
3. The client polls `GET /api/verification/status`. On `Rejected`, a retry notification is
   posted (see `GET /api/notifications/mine`); the user can call `/start` again.

`VerificationStatus`: `NotStarted=0`, `Pending=1`, `Verified=2`, `Rejected=3`.

## Seeded demo accounts (Development only)

| Role | Email | Password |
|---|---|---|
| Admin | `admin@tripnest.local` | `Admin@123456` |
| Landlord | `kwame@tripnest.local` | `Landlord@123456` |
| Landlord | `ama@tripnest.local` | `Landlord@123456` |
| Tenant | `kofi@tripnest.local` | `Tenant@123456` |
| Tenant | `yaa@tripnest.local` | `Tenant@123456` |
| Agent | `ekow@tripnest.local` | `Agent@123456` |
| Caretaker | `ebo@tripnest.local` | `Caretaker@123456` |

---

## Endpoints

**Legend:** 🌐 public (no auth) · 🔒 auth required · `[Role]` role-restricted · 🛡️ requires verified identity

### Auth — `api/auth`
| Method | Path | Access |
|---|---|---|
| POST | `/register` | 🌐 |
| POST | `/login` | 🌐 |
| POST | `/google` | 🌐 (Google ID token → sign-in/provision; requires `GoogleAuth:ClientId` config and a Google-verified email, else 400) |
| POST | `/facebook` | 🌐 (Facebook access token → sign-in/provision; requires `FacebookAuth:{AppId,AppSecret}` config and an email on the Facebook account, else 400) |
| POST | `/phone-login/send-otp` | 🌐 (body `{ phone }`; always the same generic 200 — texts a login code only if the number belongs to exactly one active account) |
| POST | `/phone-login/verify-otp` | 🌐 (body `{ phone, code }` → tokens like a normal login; marks phone verified) |
| POST | `/refresh-token` | 🌐 |
| POST | `/forgot-password` | 🌐 |
| POST | `/reset-password` | 🌐 |
| GET | `/me` | 🔒 |
| POST | `/logout` | 🔒 (revokes the refresh token) |
| POST | `/change-password` | 🔒 (also revokes existing refresh token) |
| POST | `/phone/send-otp` | 🔒 (no body → texts a code) |
| POST | `/phone/verify-otp` | 🔒 (body `{ code }` → marks phone verified) |
| POST | `/email/send-otp` | 🔒 (no body → emails a code) |
| POST | `/email/verify-otp` | 🔒 (body `{ code }` → marks email verified) |

### Verification — `api/verification`
| Method | Path | Access |
|---|---|---|
| POST | `/start` | 🔒 |
| GET | `/status` | 🔒 |

### Properties — `api/properties`
| Method | Path | Access |
|---|---|---|
| GET | `/` | 🌐 (active listings) |
| GET | `/{propertyId}` | 🌐 |
| GET | `/search?location=&minBedrooms=&maxBedrooms=` | 🌐 |
| GET | `/user/my-properties` | 🔒 |
| POST | `/` | 🔒 🛡️ (incl. `stayType`, `cancellationPolicy`) |
| PUT | `/{propertyId}` | 🔒 🛡️ |
| DELETE | `/{propertyId}` | 🔒 🛡️ |
| POST | `/{propertyId}/photos` | 🔒 🛡️ (multipart/form-data, owner only) |

### Availability — `api/properties/{propertyId}`
| Method | Path | Access |
|---|---|---|
| GET | `/availability` | 🌐 (blocked-date ranges) |
| GET | `/available-ranges?from=&to=` | 🌐 (open bookable ranges for the calendar) |
| POST | `/blocked-dates` | 🔒 `[Landlord]` 🛡️ |
| DELETE | `/blocked-dates/{blockedDateId}` | 🔒 `[Landlord]` 🛡️ |

### Walkthroughs — `api/properties`
| Method | Path | Access |
|---|---|---|
| GET | `/{propertyId}/walkthroughs` | 🌐 |
| GET | `/{propertyId}/walkthroughs/{walkthroughId}` | 🌐 |
| POST | `/{propertyId}/walkthrough` | 🔒 `[Landlord]` 🛡️ (multipart/form-data) |
| PATCH | `/{propertyId}/walkthrough/review` | 🔒 `[Agent,Admin]` 🛡️ |
| GET | `/pending-walkthroughs` | 🔒 `[Agent,Admin]` |
| DELETE | `/{propertyId}/walkthroughs/{walkthroughId}` | 🔒 `[Landlord,Admin]` 🛡️ |

### Bookings — `api/bookings`
| Method | Path | Access |
|---|---|---|
| GET | `/{bookingId}` | 🔒 (tenant or the property's landlord only) |
| POST | `/` | 🔒 (checks availability: confirmed bookings + blocked dates) |
| GET | `/user/my-bookings` | 🔒 |
| GET | `/{bookingId}/cancellation-preview` | 🔒 (refund % + amount per policy, no state change) |
| POST | `/{bookingId}/cancel` | 🔒 (owner only; tiered refund per policy, issued via the gateway) |

### Escrow — `api/escrow`
| Method | Path | Access |
|---|---|---|
| POST | `/initiate` | 🔒 (returns Paystack `checkoutUrl` + `paymentReference`) |
| POST | `/webhook` | 🌐 Paystack `x-paystack-signature` (HMAC-SHA512); unsigned/invalid → 401. Charged amount must match the booking total or the hold is rejected |
| GET | `/{id}` | 🔒 |
| POST | `/{id}/release` | 🔒 |
| POST | `/{id}/dispute` | 🔒 |
| PATCH | `/{id}/resolve-dispute` | 🔒 `[Admin]` |
| POST | `/{id}/refund` | 🔒 `[Admin]` |

### Agreements — `api/agreements`
| Method | Path | Access |
|---|---|---|
| POST | `/` | 🔒 |
| GET | `/mine` | 🔒 |
| GET | `/{id}` | 🔒 |
| POST | `/{id}/sign` | 🔒 |
| GET | `/{id}/download` | 🔒 (PDF) |

### Chat — `api/chat` (REST companion to SignalR hub `/hubs/chat`)
| Method | Path | Access |
|---|---|---|
| GET | `/conversations/mine` | 🔒 |
| POST | `/conversations` | 🔒 |
| GET | `/conversations/{id}` | 🔒 |
| GET | `/conversations/{id}/messages?page=&pageSize=` | 🔒 |
| POST | `/conversations/{id}/messages` | 🔒 |
| PATCH | `/messages/{id}/read` | 🔒 |
| PATCH | `/conversations/{id}/mark-read` | 🔒 |
| DELETE | `/conversations/{id}` | 🔒 |

### Caretakers — `api/caretakers`
| Method | Path | Access |
|---|---|---|
| GET | `/` | 🌐 |
| GET | `/{id}` | 🌐 |
| POST | `/assign` | 🔒 `[Landlord]` 🛡️ |
| POST | `/service-requests` | 🔒 |
| GET | `/service-requests/mine` | 🔒 |
| PATCH | `/service-requests/{id}/accept` | 🔒 `[Caretaker]` 🛡️ |
| PATCH | `/service-requests/{id}/status` | 🔒 |
| POST | `/service-requests/{id}/review` | 🔒 |

### Agents — `api/agents`
| Method | Path | Access |
|---|---|---|
| GET | `/` | 🌐 |
| GET | `/{id}` | 🌐 |
| POST | `/{id}/viewing-requests` | 🔒 `[Tenant]` |
| PATCH | `/viewing-requests/{id}/status` | 🔒 `[Agent,Tenant]` 🛡️ |

### Maintenance — `api/maintenance`
| Method | Path | Access |
|---|---|---|
| POST | `/` | 🔒 (report) |
| PATCH | `/{id}/status` | 🔒 |
| GET | `/property/{propertyId}` | 🔒 `[Landlord,Admin]` |
| GET | `/mine` | 🔒 `[Tenant]` |
| POST | `/{id}/convert-to-service-request` | 🔒 `[Landlord,Admin]` 🛡️ |

### Reviews — `api/reviews`
| Method | Path | Access |
|---|---|---|
| GET | `/property/{propertyId}?page=&pageSize=` | 🌐 |
| GET | `/{id}` | 🌐 |
| POST | `/` | 🔒 |
| GET | `/mine` | 🔒 |
| DELETE | `/{id}` | 🔒 |

### Notifications — `api/notifications`
| Method | Path | Access |
|---|---|---|
| GET | `/mine?page=&pageSize=` | 🔒 |
| GET | `/unread-count` | 🔒 |
| PATCH | `/{id}/read` | 🔒 |
| PATCH | `/mark-all-read` | 🔒 |
| DELETE | `/{id}` | 🔒 |

### Communication preferences — `api/communication-preferences`
SMS/email opt-out (default on). Emergency safety alerts are **always** sent regardless.
| Method | Path | Access |
|---|---|---|
| GET | `/mine` | 🔒 |
| PUT | `/mine` | 🔒 (body `{ smsEnabled, emailEnabled }`) |

### Receipts — `api/receipts`
| Method | Path | Access |
|---|---|---|
| GET | `/mine?page=&pageSize=` | 🔒 |
| GET | `/{id}` | 🔒 |
| GET | `/{id}/download` | 🔒 (PDF) |
| GET | `/booking/{bookingId}` | 🔒 |

### Wishlist — `api/wishlist`
| Method | Path | Access |
|---|---|---|
| GET | `/mine` | 🔒 |
| POST | `/{propertyId}` | 🔒 |
| DELETE | `/{propertyId}` | 🔒 |

### Profile — `api/profile`
| Method | Path | Access |
|---|---|---|
| GET | `/me` | 🔒 |
| PUT | `/me` | 🔒 |
| POST | `/photo` | 🔒 (multipart/form-data) |

### Settings — `api/settings`
| Method | Path | Access |
|---|---|---|
| GET | `/notifications` | 🔒 (pass-through to communication preferences) |
| PUT | `/notifications` | 🔒 |
| PUT | `/password` | 🔒 |
| DELETE | `/account` | 🔒 |

### Safety — `api/safety`
| Method | Path | Access |
|---|---|---|
| GET | `/contact` | 🔒 (saved trusted contact) |
| PUT | `/contact` | 🔒 (body `{ name, phone, email }`) |
| POST | `/checkin` | 🔒 (body `{ bookingId, contactPhone?, contactEmail?, shareLocation, latitude?, longitude? }` → notifies contact; location only with consent) |
| POST | `/alert` | 🔒 (body `{ bookingId }`) |

### Trust Score — `api/trustscore`
| Method | Path | Access |
|---|---|---|
| GET | `/property/{propertyId}` | 🌐 |
| GET | `/user/{userId}` | 🌐 |
| POST | `/stay-feedback` | 🔒 |

### Search — `api/search`
| Method | Path | Access |
|---|---|---|
| GET | `/?q=&type=` | 🌐 |

### Config — `api/config`
| Method | Path | Access |
|---|---|---|
| GET | `/app-info` | 🌐 (map tiles + client config) |

### Dashboards
| Method | Path | Access |
|---|---|---|
| GET | `/api/personaldashboard/tenant` | 🔒 `[Tenant]` |
| GET | `/api/personaldashboard/landlord` | 🔒 `[Landlord]` |
| GET | `/api/personaldashboard/agent` | 🔒 `[Agent]` |
| GET | `/api/personaldashboard/caretaker` | 🔒 `[Caretaker]` |
| GET | `/api/landlord/stats` | 🔒 `[Landlord]` |
| GET | `/api/landlord/earnings` | 🔒 `[Landlord]` |
| GET | `/api/landlord/properties/performance` | 🔒 `[Landlord]` |
| GET | `/api/admin/stats` | 🔒 `[Admin]` |
| GET | `/api/admin/audit-logs?userId=&limit=` | 🔒 `[Admin]` |

### Pricing & calendar — `api/pricing`, `api/calendar`
| Method | Path | Access |
|---|---|---|
| GET | `/api/pricing/{propertyId}` | 🔒 `[Landlord,Admin]` (defaults derived from listing if unset) |
| PUT | `/api/pricing/{propertyId}` | 🔒 `[Landlord,Admin]` |
| GET | `/api/calendar?propertyId=&year=&month=` | 🔒 `[Landlord,Admin]` priced month w/ weekend/blocked/maintenance/booked flags |

### Landlord workspace — `api/landlord`
| Method | Path | Access |
|---|---|---|
| GET | `/api/landlord/bookings?page=&pageSize=` | 🔒 `[Landlord,Admin]` incoming bookings (paged; incl. guests count + derived stage Upcoming/Active/Complete/Canceled) |
| GET | `/api/landlord/reservations/{bookingId}` | 🔒 `[Landlord,Admin]` reservation details: trip facts, guest, earnings breakdown (nightly rate, management fee via `Platform:ManagementFeePercent`, owner payout), guest's reviews |
| GET | `/api/landlord/tenants?page=&pageSize=` | 🔒 `[Landlord,Admin]` tenant roster (paged) |
| GET | `/api/landlord/inquiries?page=&pageSize=` | 🔒 `[Landlord,Admin]` (paged) |
| PATCH | `/api/landlord/inquiries/{id}/status` | 🔒 `[Landlord,Admin]` |

### Inquiries — `api/inquiries`
| Method | Path | Access |
|---|---|---|
| POST | `/api/inquiries` | 🔒 send a pre-booking enquiry to a listing's landlord |

### Saved payment methods — `api/payments/methods`
| Method | Path | Access |
|---|---|---|
| GET | `/api/payments/methods` | 🔒 |
| POST | `/api/payments/methods` | 🔒 |
| PATCH | `/api/payments/methods/{id}/primary` | 🔒 |
| DELETE | `/api/payments/methods/{id}` | 🔒 |

### Host tasks — `api/tasks`
| Method | Path | Access |
|---|---|---|
| GET | `/api/tasks?page=&pageSize=` | 🔒 `[Landlord,Admin]` (paged) |
| POST | `/api/tasks` | 🔒 `[Landlord,Admin]` |
| PATCH | `/api/tasks/{id}` | 🔒 `[Landlord,Admin]` |
| DELETE | `/api/tasks/{id}` | 🔒 `[Landlord,Admin]` |

### Team — `api/team`
| Method | Path | Access |
|---|---|---|
| GET | `/api/team` | 🔒 `[Landlord,Admin]` |
| POST | `/api/team` | 🔒 `[Landlord,Admin]` invite |
| PATCH | `/api/team/{id}` | 🔒 `[Landlord,Admin]` role/status |
| DELETE | `/api/team/{id}` | 🔒 `[Landlord,Admin]` |

### Statements — `api/statements`
| Method | Path | Access |
|---|---|---|
| GET | `/api/statements` | 🔒 `[Landlord,Admin]` monthly gross/fee/net payout (computed) |

### Owner Exchange — `api/exchange`
| Method | Path | Access |
|---|---|---|
| GET | `/api/exchange/posts?page=&pageSize=` | 🔒 (paged) |
| POST | `/api/exchange/posts` | 🔒 |
| GET | `/api/exchange/posts/{id}/replies` | 🔒 |
| POST | `/api/exchange/posts/{id}/replies` | 🔒 |

### Resources — `api/resources`
| Method | Path | Access |
|---|---|---|
| GET | `/api/resources` | 🔒 |
| POST | `/api/resources` | 🔒 `[Admin]` |

### Virtual tour — `api/properties/{propertyId}/tour`
| Method | Path | Access |
|---|---|---|
| GET | `/api/properties/{propertyId}/tour` | 🌐 rooms + hotspots |
| PUT | `/api/properties/{propertyId}/tour` | 🔒 `[Landlord,Admin]` owner upsert |

### Featured listings — `api/properties`
| Method | Path | Access |
|---|---|---|
| GET | `/api/properties/featured?limit=` | 🌐 home-page featured listings |

---

## Operations & scaling

- **Health:** `GET /health/live` (process up), `GET /health/ready` (Postgres gates → 503 if down;
  TripNest.Id / face-match sidecars reported as Degraded but non-gating), `GET /health` (full report).
- **Rate limiting:** global fixed window **100/min** (per user, falling back to IP) + a stricter
  **5/min** `otp` policy on the OTP send endpoints; over-limit → **429**.
- **Caching:** public, non-personalized GETs (config, properties, search, caretakers, agents, trust
  score) are output-cached (config 5 min; the rest 60 s, varying by query).
- **Telemetry:** structured logs (Serilog, trace-id correlated) + OpenTelemetry traces/metrics. Set
  `ApplicationInsights:ConnectionString` (or env `APPLICATIONINSIGHTS_CONNECTION_STRING`) to export to
  Azure Application Insights; empty = console only.
- **Multi-instance:** set `Redis:ConnectionString` to back the SignalR backplane, output cache and
  rate-limiter counters with Redis (shared across instances). Empty = in-memory, single instance.

## Real-time (SignalR)

- **Hub:** `/hubs/chat` (requires JWT). Browser clients pass the token via the
  `access_token` query string on the WebSocket handshake.
- **Server → client events:** `ReceiveMessage`, `UserTyping`, `UserStoppedTyping`.
- **Client → server methods:** `Typing`, `StopTyping` (broadcast to the other participant).
- REST `POST /api/chat/conversations/{id}/messages` also broadcasts live, so non-realtime
  clients and connected clients stay in sync.
