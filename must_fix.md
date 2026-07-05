# TripNest.Core — Code Review: Must-Fix Report

_Reviewed as senior backend developer, project manager, and tester. Date: 2026-07-05._

> ## ✅ Status: all items fixed — 2026-07-05
>
> | Item | Resolution |
> |---|---|
> | 🔴 High #1 — booking never `Confirmed` | `EscrowService.VerifyAndHoldPaymentAsync` now moves the booking `Pending → Confirmed` in the same save as the escrow hold. |
> | 🔴 High #2 — initiate returns no checkout URL | `InitiatePaymentAsync` now starts (or restarts) the provider checkout for a `Pending` escrow — including the one created with the booking — stamping `PaymentReference` and returning `CheckoutUrl`. Held/terminal escrows are still returned as-is (idempotent). |
> | 🟡 Medium — simulated verify breaks dev | `PaymentVerifyResult` gained a `Simulated` flag; the unconfigured gateway sets it, and `VerifyPaymentByBookingAsync` substitutes the escrow's expected amount so the guard passes. |
> | 🟡 Low — in-memory pagination | Notifications and property reviews now page in the database via `FindPageAsync` (with `Paging.Clamp`, newest first). |
> | 🧪 Tester's note — parallel flakiness | Assembly-level `[CollectionBehavior(DisableTestParallelization = true)]` — test classes (each a full in-process host) now run serially. Full suite: 237/237 green in ~70s. |
> | ➕ E2E coverage | New `BookingLifecycleE2ETests` drives book → initiate (checkout URL) → verify (held + Confirmed) → agreement → double-book rejected (409). |
> | ➕ Bonus (found while smoke-testing) | Bare booking dates ("2026-08-04") deserialized as `Kind=Unspecified` and made Npgsql throw on `timestamptz`, 500-ing booking creation against real Postgres (invisible to the in-memory test provider). `CreateBookingAsync` now normalizes both dates to UTC midnight. |
>
> Verified live against a running instance + PostgreSQL: full lifecycle completes and the
> double-booking rejection engages.

## Summary

Overall a well-built codebase: money paths guard idempotency and amount-tampering,
ownership/IDOR checks are consistent across services, secrets/JWT/Paystack are fail-fast
in production, and the webhook HMAC verification is correct and constant-time.

The problems cluster in one place — the **booking → pay → confirm → agreement lifecycle**,
which is wired incompletely — plus a few smaller issues. All 236 tests pass on a clean run;
the intermittent failures seen during review were parallel test-host startup flakiness, not
product bugs (see Tester's note).

---

## 🔴 High #1 — A booking is never moved to `Confirmed`, silently breaking three things

Bookings are created as `Pending` (`Services/BookingService.cs:67`). Nothing in the app ever
sets a booking to `BookingStatus.Confirmed` outside the seeder and tests. Paying
(`EscrowService.VerifyAndHoldPaymentAsync`) updates only the **escrow** row, never the booking.
The state transition is missing.

Consequences:

1. **Double-booking protection is effectively dead.**
   `AvailabilityService.IsRangeAvailable` only treats `Confirmed` bookings as blocking
   (`Services/AvailabilityService.cs:29`), and the Postgres exclusion constraint only fires
   `WHERE Status = 1` (Confirmed) — see migration
   `20260618191458_AddConcurrencyAndIntegrityConstraints`. Since no booking is ever Confirmed,
   every date range always reports available and the DB constraint never engages. Two tenants
   can book and pay for the same property and dates.
2. **Agreements can never be created.**
   `AgreementService.CreateAgreementAsync:33` throws unless the booking is `Confirmed`.
   That endpoint is currently unreachable in the normal flow.
3. **Dashboards are wrong.**
   Every "confirmed/active bookings" counter (`DashboardController.cs:67`,
   `PersonalDashboardController.cs:64/67/121`) is permanently 0.

**Fix:** move the booking to `Confirmed` when its escrow is successfully held
(in `VerifyAndHoldPaymentAsync`, in the same save as the escrow update). That single transition
re-arms all three.

---

## 🔴 High #2 — `/api/escrow/initiate` returns no checkout URL, so tenants can't pay

`CreateBookingAsync` already creates a `Pending` escrow alongside the booking
(`Services/BookingService.cs:70-80`). Then `EscrowService.InitiatePaymentAsync:46` does
"if an escrow already exists, return it" — so it **always** hits that branch, never calls
Paystack, and returns `MapToResponse(existing)` with `CheckoutUrl = null` (the checkout URL is
only produced in the create branch, `Services/EscrowService.cs:63-73`). The escrow created at
booking time also never gets a `PaymentReference`.

Net effect: the initiate endpoint is a no-op that hands back an escrow with no way to pay.

**Fix:** decide on one owner of escrow creation. Either:
- Don't create the escrow in `CreateBookingAsync` and let `InitiatePaymentAsync` own it, **or**
- Have `InitiatePaymentAsync` start the Paystack checkout and stamp `PaymentReference` / return
  the URL when it finds a reference-less `Pending` escrow.

> High #1 and #2 together mean the core guest flow — book, pay, get an agreement — doesn't
> complete end to end. Add a focused integration test that drives the whole path; the current
> suite doesn't.

---

## 🟡 Medium — Simulated Paystack verify breaks the manual verify endpoint in dev

`PaystackPaymentGateway.VerifyPaymentAsync` returns `(true, 0m)` when unconfigured
(`Services/PaystackPaymentGateway.cs:82`). `VerifyPaymentByBookingAsync` feeds that `0` into
`VerifyAndHoldPaymentAsync`, whose amount-match guard (`Services/EscrowService.cs:98`) then throws
"paid amount 0.00 does not match". So the post-checkout "verify" fallback fails in any environment
without a Paystack key (local/dev). Production is safe because it refuses to boot without the key.

**Fix:** return the escrow's expected amount in the simulated branch, or short-circuit the match
check when the gateway is unconfigured.

---

## 🟡 Low / Performance — In-memory pagination over full tables

`NotificationService.GetUserNotificationsAsync:164` and `ReviewService.GetPropertyReviewsAsync:86`
load **all** rows for a user/property, then `.Skip().Take()` in memory. Fine now, but these grow
unbounded. The codebase already has the right tool — `Repository.FindPageAsync` (DB-side paging)
is used elsewhere (e.g. `MarketplaceWorkspaceService`). Migrate these two to it.

---

## 🧪 Tester's note — The suite is flaky under parallel load, not deterministic

The full run failed 33 then 126 tests, then passed all 236 clean. Every failure was the same
shape: `TestBase..ctor → WebApplicationFactory.CreateClient → EnsureServer → CreateHost` failing
at construction. Each test class spins up a full `WebApplicationFactory` including the real hosted
background services (`EscrowAutoReleaseService`, `VerificationProcessingService`, etc.) against an
in-memory DB; under xUnit's default parallelism the concurrent host startups contend and
intermittently fail. This will cause false CI failures.

**Options:**
- Disable app-parallelization for the integration collection
  (`[CollectionDefinition(DisableParallelization = true)]` or a shared factory fixture), or
- Gate the hosted services out under the test environment.

---

## ✅ Things checked that are solid (no action needed)

- Webhook HMAC-SHA512 verified over the raw body in constant time; rejects when unconfigured
  (`EscrowController.cs:154`).
- Escrow state machine guards every transition and re-checks amount on the verify-vs-webhook race;
  refunds go through the provider before the row is marked `Refunded`.
- Ownership / IDOR checks are consistent (payouts, inquiries, host tasks, team, pricing, tours,
  chat, agreements all verify the caller owns the resource).
- Auth: bcrypt, lockout on failed logins, refresh-token **hash** stored, sessions revoked on
  password change/reset, `IsActive` re-checked per request, external sign-in requires
  provider-verified email.
- Upload validation enforces extension allowlist + size + magic-byte sniffing; both storage
  backends guard path traversal.
- Phone / OTP flows are constant-time, single-use, attempt-capped, and enumeration-safe on the
  login variant.

---

## Suggested order of work

1. Implement `Pending → Confirmed` transition on escrow hold (High #1).
2. Reconcile escrow-creation ownership so initiate returns a checkout URL (High #2).
3. Add an end-to-end test: book → pay → confirm → agreement.
4. Fix simulated-verify amount (Medium).
5. Move notifications/reviews to `FindPageAsync` (Low).
6. Stabilise the test host startup (Tester's note).
