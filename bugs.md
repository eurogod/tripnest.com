# TripNest.Core — Review & Bug Findings

_Generated: 2026-06-18 · Branch: develop_

This document captures the project analysis and the code review of the escrow
money flow and the auth/verification security posture.

---

## 1. Project overview

**TripNest.Core** is a **.NET 8 Web API** backend for an accommodation-booking
platform centred on trust, identity verification, and escrow-protected payments
(Ghana/Africa-oriented — NIA national-ID integration, "TripNest ID").

### Architecture

```
Controllers (25)  →  Services (21)  →  Repositories (17)  →  EF Core / PostgreSQL
                          ↓
                   Interfaces (DI-driven, Program.cs)
```

- **Controllers** — thin HTTP layer, JWT-secured
- **Services** — business logic, interface-backed
- **Repositories** — generic `IRepository<T>` + specific repos, EF Core over Npgsql/PostgreSQL
- **Configurations (11)** — Fluent API `IEntityTypeConfiguration`, auto-applied
- **22 entity models**, **16 enums**, **11 migrations**, per-module DTOs
- ~10,500 lines of C# (excl. bin/obj/migrations)

### Key feature modules

- **Auth** — JWT bearer + refresh tokens, BCrypt hashing, password reset, role-based
- **Verification** — NIA national-ID client + Python FastAPI sidecar (`FaceMatchService/main.py`, DeepFace) via `FaceMatchClient` over HTTP
- **Properties + Walkthroughs** — listings with video walkthroughs (IFormFile, 500 MB limit), approval gate
- **Bookings + Escrow** — escrow with grace period + `EscrowAutoReleaseService` background worker
- **Agreements, Maintenance, Reviews, Caretakers, Agents, Receipts**
- **Chat** — SignalR real-time (`ChatHub` at `/hubs/chat`) + REST
- **Trust Score** — daily snapshots via `TrustScoreDailySnapshotService`
- **Dashboards** — personal, landlord; plus Safety check-ins, Wishlist, Notifications, Search

### Cross-cutting

- 2 background hosted services (escrow auto-release, trust-score snapshots)
- Audit logging (`AuditLog` / `IAuditService`)
- Swagger with Bearer auth (dev only), CORS allow-list, auto-migrate + seed on startup
- CI (`ci.yml`) — builds with `-warnaserror`, Postgres 15 service, runs tests on push/PR to main & develop

### General observations / gaps

- Secrets committed in `appsettings.json` (JWT key, DB password) — dev defaults; CI overrides via env vars.
- Thin test coverage — only **13 tests across 5 controllers** (Auth, Properties, Escrow, Bookings, Agreements). 20 of 25 controllers and all services untested.
- `FinalModulesServices.cs` / `IMissingServices` naming suggests rushed modules; candidates for refactor.
- `Database.Migrate()` runs on every startup in all environments — risky in production.
- The Python sidecar is a separate runtime dependency not captured in the .NET build.

---

## 2. Bug findings (escrow + auth/verification)

### 🔴 Critical

**1. Escrow webhook is unauthenticated and unverified — anyone can mark any booking as paid.**
`EscrowController.cs:53` exposes `POST /api/escrow/webhook` as `[AllowAnonymous]` with **no signature/HMAC check**. It calls `VerifyAndHoldPaymentAsync` (`EscrowService.cs:46`), which verifies *nothing* — takes a `reference` string, ignores it, flips escrow to `HeldInEscrow`:
```csharp
escrow.Status = EscrowStatus.HeldInEscrow;  // no call to a payment provider
```
A stranger can curl the webhook with any `bookingId` and have the system believe payment was received.
**Fix:** verify the provider signature against the raw body and confirm the transaction server-side via `reference`.

**2. Escrow amount is caller-supplied and never reconciled with the booking.**
`InitiatePaymentAsync(bookingId, amount)` (`EscrowService.cs:25`, fed from `request.Amount` at `EscrowController.cs:40`) trusts the amount from the client body. A tenant can escrow ₵1 against a ₵1,000 booking.
**Fix:** derive the amount from the booking/property server-side.

### 🟠 High

**3. Missing state guards on admin money operations.**
`ResolveDisputeAsync` (`EscrowService.cs:128`) and `RefundEscrowAsync` (`:145`) never check current status before mutating. An admin can refund an already-`Released` escrow or resolve one never `Disputed` — a double-payout window. `ReleaseEscrowAsync` guards this correctly (`:92`); the others should too.

**4. No duplicate/idempotency guard on initiate.**
Every `InitiatePaymentAsync` call inserts a new `Escrow` with no check for an existing one on the booking. `GetByBookingIdAsync` becomes ambiguous; a booking can accumulate multiple escrows.

**5. Auto-release grace period measured from the wrong moment.**
`EscrowAutoReleaseService.cs:70` selects `e.CreatedAt < cutoffTime`. Grace is counted from escrow *creation*, not from check-out or when funds were held. An escrow created-and-held releases funds ~24h later regardless of stay dates.
**Fix:** anchor on check-out / a `HeldAt` timestamp.

### 🟡 Medium

**6. User enumeration in forgot-password.**
`ForgotPasswordAsync` (`AuthService.cs:148`) throws `"User not found"` for unknown emails, while login (`:60`) uses a generic message. Lets an attacker enumerate registered emails.
**Fix:** return success regardless of whether the email exists.

**7. Refresh tokens stored in plaintext.**
Reset tokens are BCrypt-hashed (`AuthService.cs:152`) but refresh tokens are stored raw (`:74`). A DB read leaks directly usable session tokens.
_(Good: refresh expiry IS enforced at `UserRepository.cs:31`, and rotation is implemented.)_

**8. No password-strength validation** in register/change/reset — only a match check.

**9. Verification has no rate limiting or attempt cap.**
`StartVerificationAsync` hits the paid NIA service and the face-match sidecar on every call with no throttle; `GhanaCardNumber` (PII) stored plaintext. Verification is fully auto-approved with `ReviewedAt` set to `now` (`VerificationService.cs:62`) — confirm intended vs. human review.

### Fragility / quality

**10. The `sub` claim convention is load-bearing across 73 call sites.**
`TokenService.cs:26` emits a literal `"sub"` claim; controllers read `User.FindFirst("sub")`. Works only because .NET 8's `JsonWebTokenHandler` doesn't remap inbound claims — the old `JwtSecurityTokenHandler` would rewrite `sub` → `NameIdentifier` and silently break every authenticated endpoint.
**Fix:** centralize into a single `ClaimsPrincipal.GetUserId()` extension.

**11. Error handling collapses everything to 500.**
Controllers wrap bodies in `catch (Exception)` → `InternalServerError()`, so not-found / forbidden / bad-input all surface as 500s (e.g. `EscrowController.cs:43`). Map domain exceptions to 400/403/404/409.

---

## Priority

Fix **#1** (webhook auth) and **#2** (amount trust) first — together they let an
unauthenticated caller move money. Then #3–#5 (escrow state integrity), then the
auth-hardening batch (#6–#8, #10).
