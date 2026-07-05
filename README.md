# TripNest.Core

**TripNest.Core** is the ASP.NET Core 8 Web API backend for **TripNest**, an accommodation-booking
platform centred on **trust, identity verification, and escrow-protected payments**, built for the
Ghanaian market.

Guests browse and book listings; their payment is held in **escrow** until the stay completes;
hosts (landlords, agents, caretakers) must pass **Ghana Card identity verification** before they can
list or manage properties; and money is disbursed to hosts via **Paystack Transfers**. Real-time
chat, safety check-ins, cancellation policies, reviews, and PDF agreements/receipts round out the
platform.

> **Base URL (local):** `http://localhost:5091` &nbsp;·&nbsp; **Swagger (Dev):** `/swagger` &nbsp;·&nbsp; **Health:** `/health/live`, `/health/ready`, `/health`

---

## Table of contents

- [Tech stack](#tech-stack)
- [Architecture](#architecture)
- [Repository layout](#repository-layout)
- [Getting started](#getting-started)
- [Configuration & secrets](#configuration--secrets)
- [Running the app](#running-the-app)
- [Database & migrations](#database--migrations)
- [Testing](#testing)
- [Core domain concepts](#core-domain-concepts)
- [External integrations](#external-integrations)
- [Companion services (sidecars)](#companion-services-sidecars)
- [Observability & operations](#observability--operations)
- [API reference](#api-reference)
- [Project documentation](#project-documentation)

---

## Tech stack

| Area | Technology |
|---|---|
| Runtime / framework | .NET 8, ASP.NET Core Web API |
| Database | PostgreSQL via EF Core 8 (`Npgsql`) |
| Auth | JWT Bearer + hashed refresh tokens; BCrypt password hashing |
| Real-time | SignalR (`/hubs/chat`), optional Redis backplane |
| Payments | Paystack (checkout, verify, refund, Transfers) |
| SMS / Email | TextBee (SMS) · Gmail SMTP via MailKit (email) |
| Identity verification | TripNest.Id service (Ghana Card) + Python DeepFace face-match sidecar |
| PDF / QR | QuestPDF (Community licence) + QRCoder |
| File storage | Azure Blob (multi-instance) or local disk (dev) |
| Caching / rate limiting | In-memory or Redis-backed output cache + fixed-window rate limiter |
| Phone validation | libphonenumber-csharp (E.164 normalisation) |
| Observability | Serilog (structured logs) + OpenTelemetry → Azure Application Insights |

---

## Architecture

Classic layered design wired through dependency injection in `Program.cs`:

```
Controllers  →  Services (Services/, interfaces in Interfaces/Services/)
             →  Repositories (Repositories/, generic Repository<T> + specific)
             →  EF Core / Npgsql (PostgreSQL)
```

Key conventions:

- **Shared DbContext per request.** All repositories share the scoped `AppDbContext`, so a single
  `SaveChangesAsync` commits changes made through different repositories **atomically**. Rely on
  this rather than multiple saves.
- **Uniform response envelope.** Every endpoint returns
  `ApiResponse<T>`: `{ "message": string, "statusCode": int, "data": T | null, "success": bool }`.
- **Exceptions map to status codes.** Services signal failure by throwing. `DomainException`
  subclasses (`NotFound`=404, `Validation`=400, `Conflict`=409, `Forbidden`=403,
  `TooManyRequests`=429) and bare `InvalidOperationException` (→ 400) are translated by
  `Middleware/ExceptionHandlingMiddleware.cs`. Don't hand-map status codes in controllers unless a
  flow needs special handling.
- **Integrations are interface-backed with graceful fallback.** `ISmsSender`, `IEmailSender`,
  `IPaymentGateway` log and no-op / return a stub when unconfigured, so notification and payment
  side-effects never break the underlying business action. Tests swap them for recording doubles.
- **Async, queue-driven background work.** Identity verification, non-emergency notification
  delivery, escrow auto-release, and daily trust-score snapshots run in hosted `BackgroundService`s
  fed by in-memory channels, off the HTTP request path.

---

## Repository layout

```
TripNest.Core/                 # The Web API project
├── Controllers/               # HTTP endpoints (thin; delegate to services)
├── Services/                  # Business logic + interface implementations
├── Repositories/              # EF Core data access (generic + specific)
├── Interfaces/                # Service & repository contracts
├── Models/                    # EF entities (User, Booking, Escrow, Payout, …)
├── DTOs/                      # Request/response shapes
├── Configurations/            # EF entity type configurations (indexes, constraints)
├── Migrations/                # EF Core migrations
├── Middleware/                # Exception handling, security headers
├── Filters/                   # e.g. RequireVerifiedAttribute (identity gate)
├── Hubs/                      # SignalR ChatHub + presence tracker
├── Pdf/                       # QuestPDF documents (agreement, receipt, ID card)
├── Security/                  # Password policy, token helpers
├── Storage/                   # Blob / local file storage + upload validation
├── Extensions/, Caching/, Monitoring/, Response/, Enums/, Exceptions/
├── FaceMatchService/          # Python DeepFace sidecar (runs separately on :5001)
├── Program.cs                 # DI wiring, middleware pipeline, startup
└── appsettings.json           # Non-secret localhost defaults

TripNest.Core.Tests/           # xUnit integration tests (in-memory WebApplicationFactory)
API.md                         # Maintained endpoint + integration reference
tripnest.md / CLAUDE.md        # Codebase guide for contributors/tooling
docs/, Details/                # Additional design notes
must_fix.md                    # Latest code-review findings
```

---

## Getting started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- **PostgreSQL** (local, or a cloud instance such as Azure Database for PostgreSQL)
- _(Optional)_ Redis — only for multi-instance SignalR / cache / rate limiting
- _(Optional)_ Python 3.11 + `tf-keras` (DeepFace) — only for the Ghana Card face-match sidecar

### Clone & restore

```bash
git clone https://github.com/Kluivert-K/TripNest-Core.git
cd TripNest-Core
dotnet restore
```

---

## Configuration & secrets

Configuration is layered: `appsettings.json` holds **non-secret localhost defaults**; the real
connection string and all integration keys come from **.NET user-secrets** locally (or environment
variables in CI / Azure). Never commit secrets.

```bash
# List current secrets
dotnet user-secrets list --project TripNest.Core

# Set a secret (examples)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Database=...;Username=...;Password=..." --project TripNest.Core
dotnet user-secrets set "Jwt:Key" "<a strong 32+ character secret>" --project TripNest.Core
dotnet user-secrets set "PaystackSettings:SecretKey" "sk_test_..." --project TripNest.Core
```

### Common configuration keys

| Key | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string |
| `Jwt:{Key,Issuer,Audience,ExpiryHours}` | Access-token signing & validation |
| `Cors:AllowedOrigins` | Allowed frontend origins |
| `PaystackSettings:{SecretKey,PublicKey,CallbackUrl}` | Payments (escrow + transfers) |
| `TextBeeSettings:{BaseUrl,ApiKey,DeviceId}` | SMS delivery |
| `SmtpSettings:{Host,Port,UseStartTls,Username,Password,FromEmail,FromName}` | Email delivery |
| `GoogleAuth:ClientId` / `FacebookAuth:*` | Social sign-in (empty = disabled) |
| `Services:{TripNestId,FaceMatchSidecar}` | Verification sidecar URLs |
| `Escrow:GracePeriodHours` | Auto-release window after checkout (default 24) |
| `Platform:ManagementFeePercent` | Platform fee taken from host payouts |
| `Redis:ConnectionString` | Enables multi-instance backplane/cache/limiter (empty = in-memory) |
| `Storage:Blob:{ConnectionString,Container}` | Azure Blob uploads (empty = local disk) |
| `ApplicationInsights:ConnectionString` | Ships telemetry to Azure (empty = console only) |
| `Database:AutoMigrate` | Auto-apply migrations on startup (defaults to Development) |
| `ForwardedHeaders:{KnownProxies,KnownNetworks}` | Trusted reverse proxies for real client IP |

> **Fail-fast guards.** Outside Development the app refuses to boot with a weak/default `Jwt:Key`
> (< 32 chars or the committed placeholder). In Production it refuses to boot without a Paystack
> secret key — the unconfigured gateway *simulates* successful payments and must never run against
> real bookings.

> **Graceful degradation (dev/test).** With no Paystack/SMS/email keys, payments return a simulated
> reference and SMS/email log-and-no-op, so the whole app runs locally without any credentials.

---

## Running the app

```bash
dotnet build
dotnet run --project TripNest.Core
```

Then open **`http://localhost:5091/swagger`** (Development only) for interactive API docs.

In **Development**, the database is seeded with demo accounts and listings. Seed data includes
well-known credentials and is **never** seeded outside Development.

---

## Database & migrations

Migrations are managed with EF Core. Design-time uses `Program` + user-secrets, so commands target
whatever `DefaultConnection` points at.

```bash
# Add a migration
dotnet ef migrations add <Name> --project TripNest.Core

# Apply migrations
dotnet ef database update --project TripNest.Core
```

- **Development:** migrations **auto-apply on startup** (`Database:AutoMigrate`, defaults to
  `IsDevelopment()`).
- **Production:** apply migrations **out-of-band** (CI/CD) and set `Database:AutoMigrate=false` so
  multiple instances don't race on startup.

The schema enforces integrity at the database level where it matters — e.g. a Postgres **exclusion
constraint** (`btree_gist`) prevents overlapping confirmed bookings for the same property, closing
the TOCTOU race that an app-level check alone cannot.

---

## Testing

The suite is **xUnit** and spins up the API in-memory via `WebApplicationFactory`, swapping the
external integrations for recording doubles and using the EF **in-memory provider**.

```bash
# Full suite
dotnet test

# What CI runs (excludes opt-in live integration tests)
dotnet test --filter "FullyQualifiedName!~Live"

# A single class or test
dotnet test --filter "FullyQualifiedName~PhoneVerificationTests"
dotnet test --filter "FullyQualifiedName~SafetyCheckInTests.CheckIn_NoContact_RecordsOnly_NoSend"
```

**Live integration tests** hit real TextBee/SMTP/Paystack and are skipped unless explicitly
enabled:

```bash
RUN_LIVE_INTEGRATION=1 dotnet test --filter "FullyQualifiedName~Live"
# recipients come from LiveTest:Phones / LiveTest:Emails in user-secrets
```

> **Note:** the in-memory provider does **not** enforce unique indexes, sequences, or raw SQL —
> keep persistence logic provider-agnostic. CI builds Release with `-warnaserror`, so keep the
> build warning-clean.

---

## Core domain concepts

### Two distinct "verification" concepts — do not conflate

- **`User.IsVerified` — Ghana Card identity.** Set only by `VerificationService` after a successful
  NIA lookup + face match. `Filters/RequireVerifiedAttribute.cs` gates **Landlord / Agent /
  Caretaker** actions on it (they get **403** until verified). Tenants, Guests and Admins are
  unaffected.
- **`User.EmailVerified` / `User.PhoneVerified` — contact ownership** via OTP. Independent of each
  other and of identity; on-demand and non-blocking.

### Identity verification (async, sidecar-driven)

1. `POST /api/verification/start` validates + queues the request, returns **`Pending`** immediately.
2. A background worker calls **TripNest.Id** (Ghana Card lookup) and the **face-match sidecar**,
   then marks the request `Verified` / `Rejected` and notifies the user.
3. On success it mints the public **`TripNestId`** (`TN-GH-{year}-{serial}`) in the *same save* as
   `IsVerified`, with a unique-index collision retry.
4. Clients **poll** `GetVerificationStatus` for the outcome. Core never touches the TripNest.Id
   database directly — only over HTTP.

### Escrow-protected payments & host payouts

- A booking's payment is initiated via **Paystack** and held **in escrow**; the amount is always
  derived server-side, never trusted from the client.
- Webhooks are authenticated with **HMAC-SHA512** over the raw body (constant-time compare) and the
  paid amount is re-checked against what's owed before funds are held.
- Escrow lifecycle: `Pending → HeldInEscrow → Released / Refunded / Disputed`, with idempotent
  transitions and provider-verified refunds (only marked `Refunded` if the provider accepted it).
- Held funds **auto-release** after a grace period once the stay has ended (and no dispute), then
  disburse to the host via **Paystack Transfers** to a registered transfer recipient.
- Cancellation refunds are **tiered** by the property's cancellation policy — except a
  landlord-initiated cancellation, which always refunds the tenant 100%.

### Notifications & safety

- `NotificationService` is the central dispatch: it always records an in-app notification, then
  sends SMS/email per the user's `CommunicationPreference` opt-out — **unless** it's an emergency,
  which bypasses the opt-out and flags the record. Non-emergency delivery is queued off the request
  path; emergency alerts are sent inline.
- `SafetyController` covers a saved **trusted contact** and **safe-arrival check-in** (location
  attached only with explicit per-request consent).

### Real-time chat

SignalR `ChatHub` at `/hubs/chat`: per-conversation messaging, read receipts, typing indicators,
and online/last-seen presence — scoped so only conversation partners can see each other's presence.
A Redis backplane is required to scale the hub beyond one instance.

### Documents

PDF generation via **QuestPDF** + **QRCoder** in `Pdf/`: rental agreement, payment receipt, and a
verified-member ID card at `GET /api/profile/id-card`.

---

## External integrations

| Integration | Used for | Config keys |
|---|---|---|
| **Paystack** | Escrow payments + host transfers (test/live) | `PaystackSettings:{SecretKey,PublicKey,CallbackUrl}` |
| **TextBee** | SMS notifications (Android gateway relay) | `TextBeeSettings:{BaseUrl,ApiKey,DeviceId}` |
| **SMTP (Gmail)** | Email notifications | `SmtpSettings:{Host,Port,UseStartTls,Username,Password,FromEmail,FromName}` |
| **Google / Facebook** | Social sign-in (config-gated) | `GoogleAuth:ClientId`, `FacebookAuth:*` |

All notification/payment channels degrade gracefully when unconfigured. Social sign-in returns a
"not configured" 400 when its client id is unset.

---

## Companion services (sidecars)

| Service | Default URL | Purpose |
|---|---|---|
| **TripNest.Core** (this API) | `http://localhost:5091` | Main backend |
| **TripNest.Id** | `http://localhost:5135` | Ghana Card registry used during verification |
| **Face-match** (Python / DeepFace) | `http://localhost:5001` | Compares selfie ↔ card photo |

Core runs standalone; the two sidecars are only required for the identity-verification flow. The
face-match sidecar lives in `TripNest.Core/FaceMatchService/` and needs Python 3.11 + `tf-keras`.

---

## Observability & operations

- **Structured logging** with Serilog (console always on; trace ids enriched onto every event).
- **Distributed tracing + metrics** via OpenTelemetry, exported to **Azure Application Insights**
  when `ApplicationInsights:ConnectionString` (or `APPLICATIONINSIGHTS_CONNECTION_STRING`) is set;
  otherwise instrumentation still runs but exports nowhere (so dev/test need no Azure account).
- **Health checks:** `/health/live` (liveness), `/health/ready` (readiness — Postgres gates;
  verification sidecars are reported but non-gating), `/health` (full report).
- **Rate limiting:** a global fixed-window abuse guard (100/min) plus a stricter `"otp"` policy
  (5/min) on OTP-send endpoints, with a 60s resend cooldown surfaced as 429. Redis-backed when
  configured (true global limit), else per-instance.
- **Security headers** applied on every response; HSTS + HTTPS redirect outside Development.
- **Multi-instance readiness:** SignalR backplane, output cache, rate limiter, and file storage all
  switch from in-memory/local to Redis/Azure Blob purely by configuration.

---

## API reference

The full, maintained endpoint + integration reference lives in **[`API.md`](./API.md)** — update it
whenever routes or integrations change. In Development, the interactive Swagger UI is also available
at `/swagger`.

---

## Project documentation

| File | What it covers |
|---|---|
| [`API.md`](./API.md) | Endpoint + integration reference |
| [`tripnest.md`](./tripnest.md) / [`CLAUDE.md`](./CLAUDE.md) | Codebase guide: commands, architecture, conventions, gotchas |
| [`must_fix.md`](./must_fix.md) | Latest code-review findings (must-fix items) |
| `docs/`, `Details/` | Additional design notes |
