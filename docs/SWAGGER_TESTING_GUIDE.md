# Swagger Testing Guide — TripNest.Core

A step-by-step manual test walkthrough of the whole API in Swagger UI, including how to start
the sidecars and how to exercise every module as **Admin**, **Landlord**, **Agent**, **Tenant**,
and **Caretaker**. Follow it top to bottom the first time — later phases depend on data created
in earlier ones.

> Swagger UI: **http://localhost:5091/swagger** (Development only).
> Every request/response body schema is shown inside Swagger itself; this guide gives you the
> order, the roles, the key JSON bodies, and what to expect.

---

## 0. Prerequisites

- Azure Postgres must be reachable. If requests hang or time out with connection errors, your
  IP changed — re-add it to the server firewall (`az postgres flexible-server firewall-rule create ...`).
- Payments run on the **simulated gateway** in Development (no Paystack key needed) — every
  charge "succeeds" when you call the verify endpoints. SMS/email log to console when
  TextBee/SMTP aren't configured, and OTP codes are printed in the API console log:
  look for `[Email OTP not delivered — provider unconfigured] code for ...`.
- **AI-assist features** (Phase G, notification translation, maintenance triage) need
  `Ai:Gemini:ApiKey` (free tier) or `Ai:ApiKey` (Claude) in user-secrets. Without a key they
  return a friendly **400** ("AI features are not configured") — that's expected, not a bug.
- **Walkthrough video-frame analysis** additionally needs `ffmpeg` on the server
  (`apt install ffmpeg`). Without it, the AI check falls back to listing photos and reports
  `videoFramesAnalysed: 0`.

## 1. Start the three services

**Terminal 1 — TripNest.Core (the API you'll test):**
```bash
cd ~/TripNest.Core
dotnet run --project TripNest.Core
# → http://localhost:5091  (Swagger at /swagger; migrations auto-apply in Development)
```

**Terminal 2 — TripNest.Id (Ghana Card authority, needed only for identity verification):**
```bash
cd ~/TripNest.Id        # the separate authority repo
dotnet run              # → http://localhost:5135
```

**Terminal 3 — Face-match sidecar (Python, needed only for identity verification):**
```bash
cd ~/TripNest.Core/TripNest.Core/FaceMatchService
# one-time setup (system Python is too new for TF — must use 3.11):
python3.11 -m venv venv
venv/bin/pip install -r requirements.txt
venv/bin/pip install "tf-keras==2.16.0"     # pin required — see FaceMatchService/README.md
# run:
TF_USE_LEGACY_KERAS=1 venv/bin/uvicorn main:app --host 0.0.0.0 --port 5001
```
The first face comparison downloads ~100 MB of model weights and each compare takes ~9s on CPU.

**Health check:** `GET http://localhost:5091/health/ready` — Postgres gates readiness; the two
sidecars are reported but non-gating (the API runs fine without them; only verification needs them).

## 2. How to authenticate in Swagger

1. Expand **`POST /api/auth/login`** → *Try it out* → body:
   ```json
   { "email": "kofi@tripnest.local", "password": "Tenant@123456" }
   ```
2. Copy the `accessToken` from the response.
3. Click the green **Authorize** button (top right) and paste:
   `Bearer <accessToken>` *(if the box says "bearerAuth", paste just the token)*.
4. Everything you call now runs as that user. **To switch roles, log in as another account and
   re-Authorize** — you'll do this constantly below.

### Seeded demo accounts (Development)

| Role | Email | Password | Pre-verified? |
|---|---|---|---|
| Admin | `admin@tripnest.local` | `Admin@123456` | ✅ |
| Landlord | `kwame@tripnest.local` | `Landlord@123456` | ✅ |
| Landlord | `ama@tripnest.local` | `Landlord@123456` | ✅ |
| Tenant | `kofi@tripnest.local` | `Tenant@123456` | — (tenants don't need it) |
| Tenant | `yaa@tripnest.local` | `Tenant@123456` | — |
| Agent | `ekow@tripnest.local` | `Agent@123456` | ✅ |
| Caretaker | `ebo@tripnest.local` | `Caretaker@123456` | ✅ |

Seeded landlords/agent/caretaker are **already identity-verified**, so you can do the whole
landlord/tenant flow without the sidecars. Phase 5 tests verification itself with a fresh account.

---

## 3. Phase A — Public endpoints (no login)

1. `GET /api/properties` — seeded active listings.
2. `GET /api/properties/search?location=Accra&checkIn=2026-09-07&checkOut=2026-09-10` — note
   each result's `quote` (all-in total) and headers `X-Total-Count` etc. Try the filters:
   `stayType=Student`, `amenities=WiFi,Kitchen`, `minPrice=&maxPrice=`, and map bounds
   `minLat=5.4&maxLat=5.8&minLng=-0.4&maxLng=0.1`.
3. `GET /api/properties/{propertyId}/quote?checkIn=&checkOut=` — the exact bookable total.
4. `GET /api/properties/{id}`, `GET /api/properties/{id}/walkthroughs`,
   `GET /api/properties/{id}/availability`, `GET /api/reviews/property/{id}`.
5. `GET /api/search?q=apartment` (global search), `GET /api/config/app-info`,
   `GET /api/caretakers`, `GET /api/agents`, `GET /api/trust-score/...` endpoints.

## 4. Phase B — Landlord journey (login `kwame@tripnest.local`)

1. **Create a listing** — `POST /api/properties`:
   ```json
   {
     "title": "Test 2BR East Legon", "description": "Bright and quiet",
     "location": "Accra, Ghana", "latitude": 5.65, "longitude": -0.16,
     "bedrooms": 2, "bathrooms": 2, "monthlyRent": 3000, "dailyRate": 150,
     "propertyType": "Apartment", "stayType": "ShortTerm",
     "cancellationPolicy": "Moderate", "amenities": "WiFi,Kitchen,AC"
   }
   ```
   The listing starts **Draft** — it will NOT appear in search yet.
2. **Photos** — `POST /api/properties/{id}/photos` (multipart, pick image files).
3. **AI listing copy** (optional, needs `Ai:Gemini:ApiKey`) — `POST /api/properties/{id}/generate-copy`.
4. **Pricing rules** — `PUT /api/pricing/{propertyId}`:
   ```json
   { "baseRate": 150, "weekendRate": 180, "weeklyDiscountPercent": 10,
     "monthlyDiscountPercent": 20, "minNights": 1, "cleaningFee": 40,
     "dynamicPricingEnabled": true, "minNightlyRate": 120, "maxNightlyRate": 250 }
   ```
   Then re-run the public quote — with dynamic pricing on, an empty city quotes ~0.9× base,
   clamped to your min/max.
5. **Rate calendar** — `GET /api/calendar?propertyId=...&year=2026&month=9`.
6. **Walkthrough video** — `POST /api/properties/{id}/walkthrough` (multipart, any small mp4).
   Status becomes PendingReview; the listing still can't go Active.
7. **Approve it** — log in as **Agent (`ekow`)** or **Admin**, then:
   - `GET /api/properties/pending-walkthroughs` — find yours.
   - `PATCH /api/properties/{propertyId}/walkthrough/review` → `{ "approved": true }`.
   Now log back in as kwame; the property can be Active and shows in search, and property
   responses carry `walkthroughVerifiedAt` + `walkthroughBadgeFresh`.
8. **Blocked dates** — `POST /api/properties/{id}/blocked-dates`, then confirm the range
   disappears from `GET .../available-ranges` and from dated search.
9. **Calendar sync (export)** — `GET /api/calendar/{propertyId}/feed-url` → open the returned
   `.ics` URL in a browser: your confirmed bookings + blocked dates as VEVENTs.
10. **Calendar sync (import)** — `POST /api/calendar/{propertyId}/external`
    `{ "name": "Airbnb", "feedUrl": "https://<a-real-public-ics-url>" }` → response shows
    `importedRanges` / `lastSyncError`. Also try `GET .../external`, `POST /api/calendar/external/{id}/sync`,
    `DELETE /api/calendar/external/{id}`.
11. **Payout account** — `POST /api/payouts/account` (see schema; simulated gateway accepts
    anything) then `GET /api/payouts/account`, `GET /api/payouts/mine`.
12. Landlord workspace: `GET /api/landlord/bookings`, `/tenants`, `/inquiries`,
    `GET /api/statements`, host tasks + team + tours under their sections, Owner Exchange
    (`POST /api/exchange/posts`, replies), `GET /api/resources`.

## 5. Phase C — Identity verification with the sidecars (fresh account)

Only this phase needs TripNest.Id + face-match running.

1. In **TripNest.Id** (`:5135`, its own Swagger): create a citizen → upload a **real face photo**
   → issue a card → note the returned `GHA-YYYY-NNNNNN`.
2. In Core, **register a fresh landlord**: `POST /api/auth/register` (role `Landlord`), log in.
3. `POST /api/verification/selfie` (multipart — a photo of the SAME face) → copy `selfiePhotoPath`.
4. `POST /api/verification/start`:
   ```json
   { "ghanaCardNumber": "GHA-2026-000123", "selfiePhotoPath": "<from step 3>",
     "firstName": "<citizen's>", "lastName": "<citizen's>", "dateOfBirth": "1990-01-01" }
   ```
   Returns **Pending** immediately.
5. Poll `GET /api/verification/status` until **Verified** (first run: ~1 min for model download,
   then ~10s). On success the user gets a `TripNestId` and `GET /api/profile/id-card` returns a PDF.
6. **Retry behavior** (the bug we fixed): if it's **Rejected** (e.g. a sidecar was down →
   `ServiceError`), call `/start` again with the same card — within 5 minutes you get **429**
   (retry cooldown), after that it re-queues the SAME request. A different account using the
   same card gets a clean **400**.

## 6. Phase D — Tenant journey (login `kofi@tripnest.local`)

1. **Contact verification** — `POST /api/auth/email/send-otp` (code in API console log) →
   `POST /api/auth/email/verify-otp` `{ "code": "123456" }`. Same for `/api/auth/phone/...`.
   Send twice quickly → **429** (60s cooldown).
2. **Find & quote** — dated search from Phase A; then `GET /api/properties/{id}/quote` while
   logged in — your loyalty/student discount now shows in the breakdown.
3. **Book** — `POST /api/bookings`:
   ```json
   { "propertyId": "<kwame's listing>", "checkInDate": "2026-09-07",
     "checkOutDate": "2026-09-10", "guests": 2 }
   ```
   Status **Pending**; `totalAmount` equals the quote exactly.
4. **Pay** — `POST /api/escrow/initiate` `{ "bookingId": "..." }` → returns a (simulated)
   `checkoutUrl`. Then confirm: **`POST /api/escrow/booking/{bookingId}/verify`** — with the
   simulated gateway this marks the escrow **HeldInEscrow** and the booking **Confirmed**.
   *(The real Paystack webhook can't be tested from Swagger — it requires an HMAC signature.)*
5. **Cancellation preview** — `GET /api/bookings/{id}/cancellation-preview` — within 48h of
   booking you'll see 100% / `GracePeriod` regardless of the listing's policy.
6. **Agreement** — `POST /api/agreements` `{ "bookingId": "..." }` (Draft) →
   first upload your drawn signature `POST /api/profile/signature` (multipart image; first
   upload free) → `POST /api/agreements/{id}/sign` (→ Pending) → **switch to kwame**, sign
   again (→ Signed) → `GET /api/agreements/{id}/download` — the PDF shows both signature
   images + the SHA-256 integrity footer. Try `POST /api/agreements/{id}/terminate`
   `{ "reason": "..." }` and note signature edits need password + Ghana Card + 30-day cooldown
   (`GET /api/profile/signature` shows `editableFrom`).
7. **Stay lifecycle & money out** — as tenant: `POST /api/escrow/{escrowId}/release` after the
   stay (or as landlord check `GET /api/landlord/reservations/{bookingId}`); landlord sees the
   payout in `GET /api/payouts/mine` (Pending until a payout account exists → `POST /{id}/retry`).
   `GET /api/receipts/mine` + `/api/receipts/{id}` (PDF).
8. **Review** — `POST /api/reviews` (booking must be Completed — flip it in the DB or test the
   400), `GET /api/reviews/mine`, `DELETE /api/reviews/{id}`.
9. **Loyalty** — `GET /api/loyalty/me` → Bronze; after 3 completed stays → Silver (3% shows up
   in quotes automatically).
10. **Student status** — `POST /api/auth/student/send-otp` `{ "studentEmail": "you@st.ug.edu.gh" }`
    (gmail → 400; code in console) → `verify-otp` → `GET /api/auth/student`. Quotes on
    **Student**-stayType listings now show 5% off.
11. **Chat** — `POST /api/chat/conversations` with the landlord's `otherUserId` (from the
    property's `ownerId`) → send/read messages → `GET /api/chat/conversations/mine`.
    - **Attachments & voice notes** — `POST /api/chat/conversations/{id}/messages/attachment`
      (multipart `file` + optional `caption`): send an image, a voice note (mp3/m4a/ogg/wav/webm),
      or a document (pdf/doc/docx/txt). The response's `type` reflects the kind and `mediaUrl`
      points at the stored file; it broadcasts over SignalR like a text message. Try an HTML file
      renamed `.png` → **400** (magic-byte check).
12. Notifications (`GET /api/notifications/mine`, mark read, `PUT /api/communication-preferences/mine`).
    - **Multilingual** — set the account's `PreferredLanguage` to Twi/Ga/French (via `PUT /api/profile`
      or the DB), then re-fetch `/api/notifications/mine`: titles/messages come back translated
      (needs `Ai:Gemini:ApiKey`; cached, English is untouched). The stored notification stays English.
13. Wishlist, safety (`PUT /api/safety/trusted-contact`, `POST /api/safety/check-in`),
    maintenance (`POST /api/maintenance` → note the auto-filled `triageUrgency`/`triageCategory`
    when AI is configured → landlord updates status → converts to service request),
    inquiries and tours on the property.

## 7. Phase E — Group, long-term & roommates

1. **Split billing** — as kofi: `POST /api/bookings` with
   `"splitWithEmails": ["yaa@tripnest.local"]` → response contains `shares`. Each member (log
   in as each) runs `POST /api/bookings/shares/{shareId}/pay` then `.../verify`. The booking
   confirms only after the LAST share; `GET /api/bookings/{id}/shares` shows who paid.
   Whole-booking `POST /api/escrow/initiate` on a group booking → 400.
2. **Monthly rent** — create/pick a **LongTerm** listing and book **90+ nights**: the booking
   charges only month 1; `GET /api/rent/booking/{bookingId}` shows the invoice schedule
   (final month pro-rated). Pay one: `POST /api/rent/invoices/{id}/pay` → `.../verify`; the
   landlord's payout appears immediately (fee deducted) and `GET /api/statements` counts the
   rent in its payment month. `GET /api/rent/mine` for your schedule.
3. **Roommates** — `PUT /api/roommates/me` (budget, location, university, habits) with TWO
   accounts, then `GET /api/roommates/matches` — scores 0–100, hard smoking/pets conflicts
   never appear, and browsing without your own visible profile → 400.

## 8. Phase F — Admin (login `admin@tripnest.local`)

1. `GET /api/admin/stats`, `GET /api/admin/audit-logs`.
2. **Support / urgent** — as any tenant first: `POST /api/safety/urgent`
   `{ "message": "locked out" }` → note `ticketId` + hotline + 15-min promise. As admin:
   `GET /api/admin/support-tickets` (urgent sorts first) →
   `POST /api/admin/support-tickets/{id}/ack` (SLA stamp) → `.../resolve`.
3. **Demand events** (dynamic pricing) — `POST /api/admin/demand-events`
   `{ "name": "Festival", "location": "Accra", "startDate": "2026-09-05",
   "endDate": "2026-09-12", "upliftPercent": 30 }` → re-quote a dynamic listing in Accra for
   those dates and watch the price rise (still clamped to the host's max). `GET` / `DELETE` too.
4. **Damage claims** — as **kwame** on a booking whose stay ended (status CheckedOut, within
   14 days of checkout): `POST /api/claims` (multipart: bookingId, amount ≤ 5000, description,
   photos). As the tenant: `POST /api/claims/{id}/respond`. As admin:
   `GET /api/claims/review` → `POST /api/claims/{id}/approve` `{ "approvedAmount": 200 }` —
   the landlord's **fee-free** payout appears instantly in `/api/payouts/mine`. Or `/reject`.
5. **Escrow arbitration** — `POST /api/escrow/{id}/dispute` (tenant) →
   `PATCH /api/escrow/{id}/resolve-dispute` and `POST /api/escrow/{id}/refund` (admin).
6. Walkthrough reviews (Phase B step 7) and assistant escalations round out the admin surface.
7. **AI admin briefs** (needs an AI key) — on a filed damage claim: `GET /api/claims/{id}/brief`
   (neutral summary of both sides, photos described); on a disputed escrow:
   `GET /api/escrow/{id}/dispute-brief`. Both are advisory — they never recommend a decision.
8. **Walkthrough AI check** — `GET /api/properties/{propertyId}/walkthrough/ai-check` (Agent/Admin):
   vision consistency of the walkthrough video (or photos if no ffmpeg) against the listing facts.

> **Reaching a human without AI:** `POST /api/assistant/contact-support` `{ "message": "..." }`
> files a support ticket and opens a live admin chat **without any AI call** — so "contact
> customer care" works even when the assistant returns "not configured/unavailable" (no AI key,
> rate-limited, or provider down). `POST /api/assistant/ask` is the AI Q&A path and needs a key.

## 9. Phase G — AI assist (needs `Ai:Gemini:ApiKey` or `Ai:ApiKey`)

All advisory — nothing here moves money or decides verifications; each returns a friendly 400
when no AI key is configured.

1. **Review summary** (public) — on a listing with 2+ reviews (seed some via Phase D step 8):
   `GET /api/reviews/property/{propertyId}/summary` → "what guests say" themes (cached ~24h).
2. **Natural-language search** (public) — `GET /api/properties/search/natural?q=2 bedroom in Accra
   under 500 for a weekend in September` → parsed `criteria` + matching results.
3. **Listing quality coach** (owner) — as kwame: `GET /api/properties/{propertyId}/quality-report`
   → a 0–100 completeness score (computed in code) plus AI suggestions and photo notes.
4. **Agreement summary** (parties) — on a signed agreement: `GET /api/agreements/{id}/summary` →
   plain-language explanation in your `PreferredLanguage`.
5. **Roommate match explanation** — with two roommate profiles (Phase E step 3):
   `GET /api/roommates/matches/{otherUserId}/explanation` → why you fit + what to discuss.
6. Admin briefs and the walkthrough check live in Phase F (steps 7–8).

## 10. Phase H — Caretaker (login `ebo@tripnest.local`)

1. **Directory profile** — `GET /api/caretakers/me` (404 until created) →
   `PUT /api/caretakers/me` to create/update it (makes you visible in `GET /api/caretakers`).
2. **Availability** — `PATCH /api/caretakers/me/availability` `{ "status": 1 }` (0=Active,
   1=Inactive; **2=Suspended → 400**, admin-only).
3. **Assignments & requests** — a landlord assigns you (`POST /api/caretakers/assign`); see
   `GET /api/caretakers/assignments/mine`. On a service request:
   `PATCH /api/caretakers/service-requests/{id}/accept` / `.../decline` / `.../status`.
4. **Real dashboard** — `GET /api/personaldashboard/caretaker` now returns live metrics
   (service-request counts, average rating, monthly compensation, active engagements, recent
   requests) — not the old hardcoded zeros.

## 11. What to expect when things "fail" correctly

| You did | Expect |
|---|---|
| Called a 🔒 endpoint without Authorize | 401 |
| Touched another user's record (booking, claim, share, invoice…) | 403 or 404 — never someone else's data |
| Sent two OTPs within 60s / 6th verification attempt in an hour / instant verification retry | 429 |
| Booked dates that are blocked/booked | 409 |
| Duplicate agreement / review / claim on the same booking | 409 |
| Signed an agreement whose terms changed after the first signature | 409 (tamper evidence) |
| Uploaded a chat attachment with a spoofed type (HTML renamed `.png`/`.pdf`) | 400 (magic-byte check) |
| Caretaker self-setting availability to Suspended | 400 (admin-only) |
| Any AI endpoint with no `Ai:*` key configured | 400 ("AI features are not configured") |
| Walkthrough AI check with no ffmpeg installed | 200, but `videoFramesAnalysed: 0` (photo fallback) |
| Verification `ServiceError` rejection | TripNest.Id or the face-match sidecar isn't running |
| Everything timing out | Azure firewall — your IP changed |

Rate limits are global 100/min plus a 5/min `otp` policy — if you're clicking fast in Swagger
and suddenly get 429s, that's the limiter doing its job; wait a minute.
