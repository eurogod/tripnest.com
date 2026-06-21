# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build (CI builds Release with -warnaserror — keep the build warning-clean)
dotnet build
dotnet build --configuration Release -warnaserror

# Tests (xUnit). The suite spins up the API in-memory via WebApplicationFactory.
dotnet test
dotnet test --filter "FullyQualifiedName!~Live"      # exclude opt-in live tests (what CI runs)
dotnet test --filter "FullyQualifiedName~PhoneVerificationTests"   # one class
dotnet test --filter "FullyQualifiedName~SafetyCheckInTests.CheckIn_NoContact_RecordsOnly_NoSend"  # one test

# EF Core migrations (design-time uses Program + user-secrets, so it targets whatever
# DefaultConnection points at — currently Azure Postgres)
dotnet ef migrations add <Name> --project TripNest.Core
dotnet ef database update --project TripNest.Core

# Secrets (real Azure connection string + integration keys live here, NOT appsettings.json)
dotnet user-secrets list --project TripNest.Core
dotnet user-secrets set "<key>" "<value>" --project TripNest.Core
```

**Live integration tests** hit real TextBee/SMTP/Paystack and are skipped unless enabled:
`RUN_LIVE_INTEGRATION=1 dotnet test --filter "FullyQualifiedName~Live"` (recipients come from
`LiveTest:Phones` / `LiveTest:Emails` in user-secrets).

**Face-match sidecar** (Python, only needed for Ghana Card verification): runs separately on
`:5001` — see `TripNest.Core/FaceMatchService/`. Needs python3.11 + tf-keras (DeepFace).

## Architecture

ASP.NET Core 8 Web API, classic layered design wired through DI in `Program.cs`:
**Controllers → Services (`Services/`, interfaces in `Interfaces/Services/`) → Repositories
(`Repositories/`, generic `Repository<T>` + specific ones) → EF Core / Npgsql (Postgres).**
All repositories share the scoped `AppDbContext`, so a single `SaveChangesAsync` commits changes
made through different repositories atomically — rely on this rather than multiple saves.

**Response + error convention.** Every endpoint returns `ApiResponse<T>` (`Response/ApiResponse.cs`,
`T : class`). Services signal failure by throwing: `DomainException` subclasses
(`Exceptions/DomainException.cs` — `NotFound`=404, `Validation`=400, `Conflict`=409, `Forbidden`=403,
`TooManyRequests`=429) and bare `InvalidOperationException` (treated as 400) are mapped to HTTP
status codes by `Middleware/ExceptionHandlingMiddleware.cs`. Don't hand-map status codes in
controllers unless a flow needs special handling.

**Two distinct "verification" concepts — do not conflate:**
- `User.IsVerified` = **Ghana Card identity** verification, set only by `VerificationService` after a
  successful NIA lookup + face match. `Filters/RequireVerifiedAttribute.cs` gates
  Landlord/Agent/Caretaker actions on it.
- `User.EmailVerified` / `User.PhoneVerified` = **contact ownership** via OTP, independent of each
  other and of identity. On-demand, non-blocking (`EmailVerificationService` /
  `PhoneVerificationService`, endpoints under `api/auth/email` and `api/auth/phone`).

**External integrations are interface-backed with graceful fallback.** `ISmsSender` (TextBee),
`IEmailSender` (Gmail SMTP via MailKit), `IPaymentGateway` (Paystack) — registered with
`AddHttpClient`. When unconfigured they log and return `false`/a stub instead of throwing, so
notification/payment side-effects never break the underlying business action. Tests swap these for
recording doubles (`TestFixture`). WhatsApp has been removed; if reintroduced, do it behind a new
`IWhatsAppSender` implementation.

**Identity verification is an async, sidecar-driven flow.** `VerificationService` persists a
`VerificationRequest` as Pending and enqueues it (`IVerificationQueue`); background processing calls
the **TripNest.Id authority service over HTTP** (`NiaClient`, base URL `Services:TripNestId`) for the
Ghana Card lookup and the **Python face-match sidecar** (`FaceMatchClient`, `Services:FaceMatchSidecar`).
Core never touches the TripNest.Id database directly. On success it mints the public
`TripNestId` (`TN-GH-{year}-{serial}`) via the shared `TripNestIdGenerator` in the *same* save as
`IsVerified`, with a unique-index collision retry (see `PersistOutcomeWithTripNestIdAsync`). Clients
submit then poll `GetVerificationStatus`.

**Notifications & safety.** `NotificationService` is the central dispatch: it always records an
in-app notification, then sends SMS/email per the user's `CommunicationPreference` opt-out — *unless*
`isEmergency`, which bypasses the opt-out and flags the record. `SafetyController` covers a saved
trusted contact and safe-arrival check-in (location attached only with per-request consent).

**Other cross-cutting pieces:** JWT bearer auth + hashed refresh tokens (`AuthService`,
`Security/`); rate limiting in `Program.cs` (global 100/min + an `"otp"` policy at 5/min applied to
OTP send endpoints, with a 60s resend cooldown surfaced as 429); SignalR `ChatHub` at `/hubs/chat`;
PDF generation via **QuestPDF** (Community licence set at startup in `Program.cs`) + **QRCoder** in
`Pdf/` (agreement, receipt, and verified-member ID card at `GET /api/profile/id-card`).

`API.md` is a maintained endpoint + integration reference — update it when routes or integrations change.

## Conventions & gotchas

- **Config layering:** `appsettings.json` holds non-secret localhost defaults; the real Azure
  Postgres connection string and all integration keys come from user-secrets (or env vars in CI/
  Azure). `Services:TripNestId` and the face-match sidecar default to localhost.
- **Migrations auto-apply on startup in Development** (`Database:AutoMigrate`, defaults to
  `IsDevelopment()`). In production apply out-of-band and set it false so instances don't race.
- **Tests use the EF in-memory provider** (`TestFixture`), which does *not* enforce unique indexes,
  sequences, or raw SQL. Keep persistence logic provider-agnostic; behaviour relying on Postgres
  semantics (e.g. unique-index collisions) won't be exercised by the in-memory suite.
- **CI builds with `-warnaserror`** and `GenerateDocumentationFile=true` (XML-doc warning 1591 is
  suppressed). A new public type/member is fine without docs, but other warnings fail the build.
