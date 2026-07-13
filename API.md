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
| GET | `/student` | 🔒 student status (email, active flag, verified/expires dates) |
| POST | `/student/send-otp` | 🔒 (body `{ studentEmail }` — must be an academic domain per `Student:AcademicDomainSuffixes`; code goes to the student mailbox) |
| POST | `/student/verify-otp` | 🔒 (body `{ code }` → verified student for `Student:ValidityDays` (365); unlocks `Student:DiscountPercent` (5%) on Student-stayType listings — the larger of student/loyalty discount applies, never stacked) |

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
| GET | `/search?location=&minBedrooms=&maxBedrooms=&stayType=&propertyType=&amenities=&minPrice=&maxPrice=&minLat=&maxLat=&minLng=&maxLng=&checkIn=&checkOut=&page=&pageSize=` | 🌐 (paged in the DB; `data` = array of properties, pagination via `X-Total-Count`/`X-Page`/`X-Page-Size`/`X-Total-Pages` headers; pageSize default/max 100; case-insensitive location match; amenities = CSV, all required; min/max Lat/Lng = map viewport; checkIn/checkOut filter to available listings and attach a per-result `quote` with the all-in stay total) |
| GET | `/{propertyId}/quote?checkIn=&checkOut=` | 🌐 (true-total price breakdown: nightly subtotal incl. weekend rates, cleaning fee, length-of-stay discount, and the caller's loyalty discount when authenticated — the exact amount booking charges) |
| GET | `/user/my-properties` | 🔒 |
| POST | `/` | 🔒 🛡️ (incl. `stayType`, `cancellationPolicy`) |
| PUT | `/{propertyId}` | 🔒 🛡️ |
| DELETE | `/{propertyId}` | 🔒 🛡️ |
| POST | `/{propertyId}/photos` | 🔒 🛡️ (multipart/form-data, owner only) |
| POST | `/{propertyId}/generate-copy` | 🔒 🛡️ (owner only; AI-drafted `{title, description, highlights}` from facts + photos, for review — never auto-applied; 400 with a clear message when no AI provider key is configured; `Ai:Provider` selects claude or gemini) |

Property responses carry `walkthroughVerifiedAt` + `walkthroughBadgeFresh` (approval within `Walkthrough:BadgeValidityDays`, 365) — clients show the "Verified" badge only while fresh and prompt hosts to re-submit after.

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
| POST | `/` | 🔒 (checks availability: confirmed bookings + blocked dates; long-term stays (LongTerm/Student listing, 60+ nights) charge only the first 30-day period upfront and generate a monthly rent-invoice schedule for the rest (see `api/rent`); optional `splitWithEmails` creates a group booking — the total splits equally into per-member shares, booker absorbs rounding, and the booking confirms only when every share is paid within `Booking:SplitPaymentWindowHours`, default 24h, else it is cancelled and paid shares refunded) |
| GET | `/user/my-bookings` | 🔒 |
| GET | `/{bookingId}/cancellation-preview` | 🔒 (refund % + amount per policy, no state change; a platform-wide grace period — `Platform:CancellationGraceHours`, default 48h after booking while check-in is ≥2 days out — refunds 100% regardless of the listing policy, reported as policyName `GracePeriod`) |
| POST | `/{bookingId}/cancel` | 🔒 (owner only; tiered refund per policy, issued via the gateway) |
| GET | `/{bookingId}/shares` | 🔒 (group members + the property's landlord) who owes what and who has paid |
| POST | `/shares/{shareId}/pay` | 🔒 (share owner only) starts the member's own provider checkout for their slice |
| POST | `/shares/{shareId}/verify` | 🔒 (share owner only) actively confirms the share with the provider; the last share confirms the booking and holds the escrow |

### Escrow — `api/escrow`
| Method | Path | Access |
|---|---|---|
| POST | `/initiate` | 🔒 (returns Paystack `checkoutUrl` + `paymentReference`; 400 for group bookings — members pay per-share instead) |
| POST | `/webhook` | 🌐 Paystack `x-paystack-signature` (HMAC-SHA512); unsigned/invalid → 401. Charged amount must match the booking total or the hold is rejected |
| GET | `/mine?page=&pageSize=` | 🔒 (paged; the caller's escrows as paying tenant, newest first) |
| GET | `/{id}` | 🔒 |
| POST | `/{id}/release` | 🔒 |
| POST | `/{id}/dispute` | 🔒 |
| PATCH | `/{id}/resolve-dispute` | 🔒 `[Admin]` |
| POST | `/{id}/refund` | 🔒 `[Admin]` |

### Agreements — `api/agreements`
| Method | Path | Access |
|---|---|---|
| POST | `/` | 🔒 |
| GET | `/mine?page=&pageSize=` | 🔒 (paged) |
| GET | `/{id}` | 🔒 |
| POST | `/{id}/sign` | 🔒 (each party signs from their own account; the first signature captures a SHA-256 hash of the terms and the second refuses to bind if the text changed — tamper evidence; the signer's profile signature image is snapshotted onto the agreement at that moment) |
| GET | `/{id}/download` | 🔒 (PDF — drawn signature images in each party's block when on file, plus a document-integrity footer with the terms hash) |
| POST | `/{id}/terminate` | 🔒 (either party) `{reason}` — Signed → Terminated, reason appended to the terms (record-keeping; money flows stay in booking/escrow) |

Agreements expire with the stay: `ExpiryDate` = the booking's checkout, and a Signed agreement past it flips to **Expired** lazily on the next read.

### Chat — `api/chat` (REST companion to SignalR hub `/hubs/chat`)
| Method | Path | Access |
|---|---|---|
| GET | `/conversations/mine?page=&pageSize=` | 🔒 (paged) |
| POST | `/conversations` | 🔒 |
| GET | `/conversations/{id}` | 🔒 |
| GET | `/conversations/{id}/messages?page=&pageSize=` | 🔒 |
| POST | `/conversations/{id}/messages` | 🔒 (scanned for off-platform-payment attempts — warns the recipient in-app, never blocks the message) |
| POST | `/conversations/{id}/messages/attachment` | 🔒 multipart `{file, caption?}` — image, voice note (mp3/m4a/aac/ogg/wav/webm) or document (pdf/doc/docx/txt); type inferred from the file, validated by extension + magic bytes + size (25 MB audio/doc, 10 MB image); `mediaUrl`/`mediaType` on the message, broadcast over SignalR like a text message |
| POST | `/conversations/{id}/suggest-reply` | 🔒 (participant only; AI-drafted reply from the linked listing's facts, for the user to edit and send; 400 when AI unconfigured; rate-limited `ai`) |
| PATCH | `/messages/{id}/read` | 🔒 |
| PATCH | `/conversations/{id}/mark-read` | 🔒 |
| DELETE | `/conversations/{id}` | 🔒 |

### Assistant — `api/assistant`
| Method | Path | Access |
|---|---|---|
| POST | `/ask` | 🔒 (AI Q&A grounded in platform rules + the caller's own bookings/escrow/verification, answered in their `preferredLanguage`; when a human is needed it opens a **live chat with an admin** — response returns `supportConversationId` — and files a support ticket; 400 when AI unconfigured; rate-limited `ai`) |
| GET | `/history?limit=` | 🔒 (the caller's assistant conversation, oldest first) |

### Caretakers — `api/caretakers`
| Method | Path | Access |
|---|---|---|
| GET | `/?serviceType=&area=&page=&pageSize=` | 🌐 (paged; Active directory profiles with rating aggregates — see PUT `/me`) |
| GET | `/me` | 🔒 `[Caretaker]` own directory profile (404 until created) |
| PUT | `/me` | 🔒 `[Caretaker]` 🛡️ create/update own directory profile (responsibilities, bio, area, rate) — required to appear in the list / be assignable |
| GET | `/{id}` | 🌐 (includes `averageRating`/`reviewCount` from service-request reviews) |
| POST | `/assign` | 🔒 `[Landlord]` 🛡️ (owner only; creates an active `PropertyCaretakerAssignment` — a caretaker can hold several; 409 if already assigned) |
| POST | `/unassign` | 🔒 `[Landlord]` 🛡️ (ends the active assignment; 404 if none) |
| GET | `/assignments/mine?page=&pageSize=` | 🔒 (paged; assignments on the caller's properties and/or as the caretaker) |
| POST | `/service-requests` | 🔒 (`propertyId` optional only when the caretaker serves exactly one property) |
| GET | `/service-requests/mine?page=&pageSize=` | 🔒 (paged) |
| PATCH | `/service-requests/{id}/accept` | 🔒 `[Caretaker]` 🛡️ (Pending → Accepted) |
| PATCH | `/service-requests/{id}/decline` | 🔒 `[Caretaker]` 🛡️ (Pending → Declined) |
| PATCH | `/service-requests/{id}/status` | 🔒 (role-gated transitions — caretaker: Accepted→InProgress/Completed; requester: Pending/Accepted→Cancelled; anything else 400) |
| POST | `/service-requests/{id}/review` | 🔒 (requester only, Completed only, rating 1–5) |

Status changes, new requests, reviews, and (un)assignments notify the counterparty via `NotificationService`.

### Agents — `api/agents`
| Method | Path | Access |
|---|---|---|
| GET | `/?serviceArea=&page=&pageSize=` | 🌐 (paged; Active directory profiles with rating aggregates — see PUT `/me`) |
| GET | `/me` | 🔒 `[Agent]` own directory profile (404 until created) |
| PUT | `/me` | 🔒 `[Agent]` 🛡️ create/update own directory profile (licence, bio, rates, service area) — required to appear in the list |
| GET | `/{id}` | 🌐 (includes `averageRating`/`reviewCount` from viewing reviews) |
| POST | `/{id}/viewing-requests` | 🔒 `[Tenant]` (must be scheduled in the future; notifies the agent) |
| GET | `/viewing-requests/mine?page=&pageSize=` | 🔒 (paged; as requesting tenant and/or assigned agent) |
| PATCH | `/viewing-requests/{id}/status` | 🔒 `[Agent,Tenant]` 🛡️ (role-gated transitions — agent: Pending→Confirmed/Declined, Confirmed→Completed; tenant: Pending/Confirmed→Cancelled; anything else 400) |
| PATCH | `/viewing-requests/{id}/decline` | 🔒 `[Agent]` 🛡️ (Pending → Declined) |
| POST | `/viewing-requests/{id}/review` | 🔒 `[Tenant]` (requester only, Completed only, rating 1–5) |

### Payouts — `api/payouts` (host disbursements via Paystack Transfers)
| Method | Path | Access |
|---|---|---|
| GET | `/account` | 🔒 `[Landlord,Agent]` own payout destination (masked; 404 until registered) |
| PUT | `/account` | 🔒 `[Landlord,Agent]` register MoMo wallet (`mobile_money`: MTN/ATL/VOD) or bank (`ghipss`) — validated with Paystack as a transfer recipient |
| GET | `/mine?page=&pageSize=` | 🔒 `[Landlord,Agent]` own payouts, newest first, paged (gross, fee, net, status) |
| POST | `/{id}/retry` | 🔒 `[Landlord,Agent]` re-attempt a Pending/Failed payout |

Escrow release (manual, auto after checkout+grace, or dispute-approved) creates one payout per
escrow (net of `Platform:ManagementFeePercent`) and initiates the transfer when the host has an
account. Paystack `transfer.success` / `transfer.failed` / `transfer.reversed` webhooks (same
signed `/api/escrow/webhook` endpoint; transfer reference = payout id) drive it to Paid/Failed,
notifying the host either way.

### Maintenance — `api/maintenance`
| Method | Path | Access |
|---|---|---|
| POST | `/` | 🔒 (report) |
| PATCH | `/{id}/status` | 🔒 |
| GET | `/property/{propertyId}?page=&pageSize=` | 🔒 `[Landlord,Admin]` (paged) |
| GET | `/mine?page=&pageSize=` | 🔒 `[Tenant]` (paged) |
| POST | `/{id}/convert-to-service-request` | 🔒 `[Landlord,Admin]` 🛡️ |

### Reviews — `api/reviews`
| Method | Path | Access |
|---|---|---|
| GET | `/property/{propertyId}?page=&pageSize=` | 🌐 |
| GET | `/{id}` | 🌐 |
| POST | `/` | 🔒 |
| GET | `/mine?page=&pageSize=` | 🔒 (paged) |
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

### Urgent support — `api/safety`
| Method | Path | Access |
|---|---|---|
| POST | `/api/safety/urgent` | 🔒 `{message}` — locked out / unsafe NOW: creates a queue-jumping urgent ticket, pages every admin via the emergency channel (opt-outs bypassed), and returns `Support:UrgentHotline` + the promised `Support:UrgentResponseMinutes` (15). Urgent tickets sort first in the admin queue; the ack endpoint stamps first response for SLA tracking. |

### Wishlist — `api/wishlist`
| Method | Path | Access |
|---|---|---|
| GET | `/mine` | 🔒 |
| POST | `/{propertyId}` | 🔒 |
| DELETE | `/{propertyId}` | 🔒 |

### Damage-protection claims — `api/claims`
| Method | Path | Access |
|---|---|---|
| POST | `/` | 🔒 `[Landlord,Agent,Admin]` multipart `{bookingId, amount ≤ Claims:MaxAmount, description, photos[]}` — one claim per booking, within `Claims:FilingWindowDays` (14) of checkout; tenant notified |
| GET | `/mine?page=&pageSize=` | 🔒 (paged) the caller's filed claims |
| GET | `/booking/{bookingId}` | 🔒 the claim on a booking (its landlord/tenant/admin) |
| POST | `/{id}/respond` | 🔒 (tenant, once) `{response}` — the tenant's side before review |
| GET | `/review?page=&pageSize=` | 🔒 `[Admin]` claims awaiting a decision, oldest first |
| POST | `/{id}/approve` | 🔒 `[Admin]` `{approvedAmount?, note?}` — pays the host **immediately and fee-free** through the standard transfer machinery |
| POST | `/{id}/reject` | 🔒 `[Admin]` `{reason}` |

### Monthly rent — `api/rent` (long-term stays)
| Method | Path | Access |
|---|---|---|
| GET | `/mine?page=&pageSize=` | 🔒 (paged) the caller's rent invoices across bookings, soonest due first |
| GET | `/booking/{bookingId}` | 🔒 (tenant or landlord) the booking's full schedule — 30-day periods at the listing's monthly rent, final partial month pro-rated |
| POST | `/invoices/{id}/pay` | 🔒 (tenant only) checkout for one month's rent (provider metadata `rent:{id}` routes the webhook) |
| POST | `/invoices/{id}/verify` | 🔒 (tenant only) actively confirm with the provider (webhook fallback) |

A paid invoice immediately creates the landlord's payout net of `Platform:ManagementFeePercent`
(no escrow hold — the tenant already lives there). A twice-daily sweep flips invoices to **Due**
inside `Rent:DueReminderDays` (default 3) and to **Overdue** past the due date, notifying both
parties. Cancelling the booking voids outstanding invoices; rent charged against a voided invoice
is auto-refunded.

### AI assist — advisory features on `IAiClient` (Gemini/Claude; friendly 400 when no key)
| Method | Path | Access |
|---|---|---|
| GET | `/api/reviews/property/{id}/summary` | 🌐 "what guests say" themes from the listing's reviews (needs 2+, cached ~24h) |
| GET | `/api/properties/search/natural?q=` | 🌐 phrase → structured filters → the real search; response echoes the parsed criteria |
| GET | `/api/properties/{id}/quality-report` | 🔒 (owner) deterministic completeness checks + 0–100 score computed in code, AI suggestions + photo notes (vision) |
| GET | `/api/agreements/{id}/summary` | 🔒 (parties) plain-language explanation in the caller's `PreferredLanguage` |
| GET | `/api/roommates/matches/{otherUserId}/explanation` | 🔒 why a pairing fits + what to discuss (cached per pair) |
| GET | `/api/claims/{id}/brief` | 🔒 `[Admin]` neutral reading brief: both sides + photo evidence described (vision); never recommends an amount |
| GET | `/api/escrow/{id}/dispute-brief` | 🔒 `[Admin]` neutral brief from the dispute note + audit trail |
| GET | `/api/properties/{propertyId}/walkthrough/ai-check` | 🔒 `[Agent,Admin]` vision consistency check: samples frames from the walkthrough VIDEO (ffmpeg; falls back to listing photos when ffmpeg is absent — `videoFramesAnalysed` reports which) vs the listing facts |

Maintenance reports are auto-triaged at creation (`triageUrgency` Low/Medium/High/Emergency +
`triageCategory` trade label, whitelisted — hallucinated labels are dropped; null when AI is off).
All AI output is **advisory**: it never drives money movement, verification outcomes, or approvals.

**Notifications** are stored in English (write path never calls AI) and translated into the reader's
`PreferredLanguage` at read time on `GET /api/notifications/mine` — cached ~30 days per text+language,
falling back to English when AI is off or a translation fails.

### Roommate matching — `api/roommates`
| Method | Path | Access |
|---|---|---|
| GET | `/me` | 🔒 own roommate profile (404 until created) |
| PUT | `/me` | 🔒 create/update profile: bio, university, preferred location, monthly budget, move-in date, habits (smoking/pets/night-owl/cleanliness), visibility |
| DELETE | `/me` | 🔒 remove profile (also removes matching access) |
| GET | `/matches?location=&maxBudget=&university=&page=&pageSize=` | 🔒 (paged) compatibility-ranked matches, best first; requires the caller's own **visible** profile (reciprocal); smoking/pets hard conflicts are excluded outright; score 0–100 = budget proximity + location overlap + same university + sleep schedule + cleanliness; each match carries the user's identity-verification badge |

From a match: start a chat (`POST api/chat/conversations`) and later book together with split billing (`splitWithEmails`).

### Loyalty — `api/loyalty`
| Method | Path | Access |
|---|---|---|
| GET | `/me` | 🔒 tier (Bronze 0+ / Silver 3+ / Gold 6+ / Platinum 10+ completed stays), active stay-discount % (0/3/5/8 — platform-funded, applied to quotes and booking totals), and progress to the next tier |

### Profile — `api/profile`
| Method | Path | Access |
|---|---|---|
| GET | `/me` | 🔒 |
| PUT | `/me` | 🔒 (incl. `preferredLanguage`: 0=English, 1=Twi, 2=Ga, 3=French — used for AI-generated text) |
| POST | `/photo` | 🔒 (multipart/form-data) |
| GET | `/signature` | 🔒 own signature status (on file?, last set, editable-from date); the image itself is never served via API — it only appears inside agreement PDFs |
| POST | `/signature` | 🔒 multipart image. First upload is free; changing it requires the account password + the Ghana Card number (verified identities) + `Profile:SignatureEditCooldownDays` (30 days) since the last change |

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
| GET | `/api/admin/support-tickets?page=&pageSize=` | 🔒 `[Admin]` (paged; open assistant escalations, oldest first) |
| POST | `/api/admin/support-tickets/{ticketId}/resolve` | 🔒 `[Admin]` (marks resolved, notifies the user; idempotent) |
| POST | `/api/admin/support-tickets/{ticketId}/ack` | 🔒 `[Admin]` stamps first response (urgent-SLA clock; idempotent) |
| GET | `/api/admin/demand-events` | 🔒 `[Admin]` demand events feeding dynamic pricing |
| POST | `/api/admin/demand-events` | 🔒 `[Admin]` `{name, location, startDate, endDate, upliftPercent ≤200}` — matching dynamically priced listings get the uplift for those dates |
| DELETE | `/api/admin/demand-events/{id}` | 🔒 `[Admin]` |

### Pricing & calendar — `api/pricing`, `api/calendar`
| Method | Path | Access |
|---|---|---|
| GET | `/api/pricing/{propertyId}` | 🔒 `[Landlord,Admin]` (defaults derived from listing if unset) |
| PUT | `/api/pricing/{propertyId}` | 🔒 `[Landlord,Admin]` (incl. `dynamicPricingEnabled` + `minNightlyRate`/`maxNightlyRate` bounds — opt-in demand pricing: nightly rates flex with same-city occupancy (0.9×–1.3×), a last-minute fill discount inside 7 days (0.9×), and admin demand events, always clamped to the host's floor/ceiling, defaults 70%/150% of base; applied identically in search quotes, the quote endpoint, and booking charges) |
| GET | `/api/calendar?propertyId=&year=&month=` | 🔒 `[Landlord,Admin]` priced month w/ weekend/blocked/maintenance/booked flags |
| GET | `/api/calendar/{propertyId}/feed-url` | 🔒 `[Landlord,Admin]` (owner only) tokenized public iCal URL — paste into Airbnb/VRBO/Booking.com "import calendar" to prevent double-bookings |
| GET | `/api/calendar/{propertyId}.ics?token=` | 🌐 (token-authorized) RFC 5545 feed of confirmed stays + blocked ranges |
| POST | `/api/calendar/{propertyId}/external` | 🔒 `[Landlord,Admin]` (owner only) link an external iCal feed (`{name, feedUrl}` — Airbnb/VRBO/Booking.com export URL; http(s) + public hostname only); imports immediately, fetch failures reported via `lastSyncError` |
| GET | `/api/calendar/{propertyId}/external` | 🔒 `[Landlord,Admin]` (owner only) linked feeds with sync status + imported-range counts |
| POST | `/api/calendar/external/{id}/sync` | 🔒 `[Landlord,Admin]` (owner only) re-import one feed now (a background worker also re-imports all feeds every `Calendar:ExternalSyncMinutes`, default 60) |
| DELETE | `/api/calendar/external/{id}` | 🔒 `[Landlord,Admin]` (owner only) unlink; removes that feed's imported blocked dates, manual blocks stay |

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
| GET | `/api/statements?page=&pageSize=` | 🔒 `[Landlord,Admin]` monthly gross/fee/net payout (computed, paged; bookings by check-in month **plus paid monthly-rent invoices by payment month**, so long-term tenancies aren't understated to their first month) |

### Owner Exchange — `api/exchange`
| Method | Path | Access |
|---|---|---|
| GET | `/api/exchange/posts?page=&pageSize=` | 🔒 (paged) |
| POST | `/api/exchange/posts` | 🔒 |
| GET | `/api/exchange/posts/{id}/replies?page=&pageSize=` | 🔒 (paged) |
| POST | `/api/exchange/posts/{id}/replies` | 🔒 |

### Resources — `api/resources`
| Method | Path | Access |
|---|---|---|
| GET | `/api/resources?page=&pageSize=` | 🔒 (paged) |
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
