# TripNest.Core — Reverse-Engineered Architecture

> A full teardown of the backend: what runs, how a request flows, how the data
> layer works, and how the key business flows are stitched together.
> Companion docs: [`tripnest.md`](../tripnest.md) (commands/conventions),
> [`FRONTEND_INTEGRATION.md`](./FRONTEND_INTEGRATION.md) (frontend handoff),
> [`identity.md`](./identity.md) (TripNest.Id service).

## 1. System landscape

TripNest is an accommodation booking platform for Ghana. The backend is one
ASP.NET Core 8 Web API (`TripNest.Core`) surrounded by focused satellites:

```
                       ┌────────────────────┐
   React SPA ── HTTP ─▶│   TripNest.Core    │── Npgsql ──▶ PostgreSQL (Azure Flexible Server)
   (frontend team)     │   ASP.NET Core 8   │── optional ▶ Redis (SignalR backplane, rate limit, output cache)
        │              └────────┬───────────┘── optional ▶ Azure Blob (uploads) / local wwwroot
        └── WebSocket ─▶ /hubs/chat (SignalR)│
                                             ├── HTTP ──▶ TripNest.Id  :5135  (Ghana Card / NIA records)
                                             ├── HTTP ──▶ Face-match sidecar :5001 (Python FastAPI + DeepFace)
                                             ├── HTTP ──▶ Paystack (escrow payments, webhook back in)
                                             ├── HTTP ──▶ TextBee (SMS OTP) / SMTP (email)
                                             └── OTLP ──▶ Azure Application Insights (optional)
```

Two hard boundaries worth knowing:

- **TripNest.Id is a separate service with its own DB.** Core only *reads*
  identity data over HTTP (`NiaClient`); it never touches that database.
- **The face-match sidecar is stateless.** Core sends two images, gets back a
  similarity + liveness score. All verification state lives in Core's DB.

## 2. Solution layout

```
TripNest.Core/
├── Controllers/      38 API controllers (thin: auth guard → service call → ApiResponse)
├── Services/         Business logic; one class per domain + 4 hosted background services
├── Interfaces/       Services/ + Repositories/ contracts (DI-first, test-friendly)
├── Repositories/     Generic Repository<T> + 18 specific repositories over EF Core
├── Models/           26 entity classes (string GUID PKs)
├── Configurations/   IEntityTypeConfiguration per entity (auto-discovered)
├── Context/          AppDbContext
├── DTOs/             Request/response records per module (+ Shared/PagedResult)
├── Enums/            Domain enums (serialized as integers)
├── Middleware/       ExceptionHandlingMiddleware (exception → status code + envelope)
├── Filters/          [RequireVerified] action filter (identity-gates host actions)
├── Hubs/             ChatHub (SignalR)
├── Response/         ApiResponse<T> envelope + static factories
├── Security/         PasswordPolicy
├── Caching/          RedisOutputCacheStore
├── Storage/          IFileStorage → BlobFileStorage | LocalFileStorage + UploadValidation
├── Monitoring/       Health checks, ActivityEnricher (trace ids in logs)
├── Pdf/              QuestPDF agreement/receipt rendering
├── FaceMatchService/ The Python sidecar (FastAPI + DeepFace) — deployed separately
└── Migrations/       EF Core migrations (raw SQL for constraints Postgres needs)
TripNest.Core.Tests/  xUnit integration tests (182) via WebApplicationFactory
```

**Layering rule:** Controller → Service interface → Repository interface → EF.
Controllers never see `AppDbContext`; services never build HTTP responses.

## 3. Request lifecycle

Middleware order (from `Program.cs`) is load-bearing:

```
ForwardedHeaders → ExceptionHandlingMiddleware → SerilogRequestLogging
→ HSTS/HTTPS redirect → StaticFiles (/uploads) → CORS → OutputCache
→ Authentication (JWT) → Authorization → RateLimiter → endpoint
```

- **Every response is enveloped** as `ApiResponse<T>`:
  `{ "message": string, "statusCode": int, "data": T|null, "success": bool }`
  (camelCase). Success factories: `Ok/Created`; failures: `NotFound/BadRequest/
  Forbidden/UnAuthorized/Conflict/TooManyRequests/InternalServerError`.
- **Exceptions become status codes centrally** (`ExceptionHandlingMiddleware`):
  `DomainException` subclasses carry their own code (`NotFoundException` 404,
  `ValidationException` 400, `ConflictException` 409, `ForbiddenException` 403,
  `TooManyRequestsException` 429); plain `InvalidOperationException` → 400,
  `UnauthorizedAccessException` → 403, `ArgumentException` → 400,
  `DbUpdateConcurrencyException` → 409, anything else → 500 (logged as error).
  Legacy controllers still carry their own try/catch (they remap
  `InvalidOperationException` → 404 in ~39 places); newer controllers rely on
  the middleware only.
- **Rate limiting:** global fixed-window 100 req/min per user-or-IP; a stricter
  `otp` policy (5/min) on OTP sends. Redis-backed when configured → a true
  cross-instance limit.
- **Output cache:** only anonymous, identical-for-everyone GETs opt in
  (`config` 5 min, `listings` 60 s varied by query string).

## 4. AuthN / AuthZ

- **JWT bearer**, HMAC-SHA256, `ClockSkew=0`. Claims: `sub` (user id), email,
  name, role. Access token lifetime `Jwt:ExpiryHours` (8h dev). App **refuses
  to boot** outside Development with the placeholder/short signing key.
- **Refresh tokens:** 32 random bytes, 7-day expiry, stored **SHA-256 hashed**;
  lookup checks expiry in SQL. One token per user (new login revokes the old
  session). Password change/reset also revokes it.
- **Passwords:** BCrypt + `PasswordPolicy`. Reset tokens are BCrypt-hashed,
  1-hour expiry, emailed; the API never reveals whether an email exists.
- **Roles** (int enum): Tenant, Landlord, Agent, Caretaker, Admin. Admin cannot
  self-register — it is seeded (dev) or promoted in the DB. Route guards via
  `[Authorize(Roles="Landlord,Admin")]` etc.
- **`[RequireVerified]`**: Landlord/Agent/Caretaker *write* actions (create
  listing, accept work…) additionally require a completed Ghana Card identity
  verification. Tenants can browse/book unverified.
- SignalR can't send an Authorization header on the WS handshake, so the JWT
  is accepted from `?access_token=` for `/hubs/*` paths only.

## 5. Data layer

- **Entities:** `string Id = Guid.NewGuid().ToString()` PKs
  (`ValueGeneratedNever`), FK string + optional nav property, `CreatedAt`
  defaulted to `NOW()` in SQL. Money `HasPrecision(18,2)`, percentages `(5,2)`.
- **Repository pattern:** open-generic `IRepository<T>` → `Repository<T>`
  (registered for any entity) + specific repositories (`IUserRepository`…)
  that inherit it. Reads are `AsNoTracking`; `UpdateAsync` reattaches via
  `_dbSet.Update`. `FindPageAsync(predicate, orderBy, page, pageSize)` runs
  filter/order/count/slice in SQL — use it for any growth-prone list.
- **Concurrency:** Postgres `xmin` mapped as a concurrency token on hot
  entities → conflicting saves throw `DbUpdateConcurrencyException` → 409.
- **Integrity where it matters most:** a `btree_gist` **exclusion constraint**
  (`no_overlapping_confirmed_bookings`) makes double-booking impossible at the
  DB level, regardless of application races.
- **Migrations:** auto-apply on startup in Development (`Database:AutoMigrate`
  overridable); production is expected to migrate out-of-band. Demo seeding
  (well-known credentials) runs in Development only.
- **Pagination contract:** `PagedResult<T>` = `items, totalCount, page,
  pageSize, totalPages`; page size clamped to 100.

## 6. Domain modules (controller → what it does)

**Identity & account**
| Route base | Purpose |
|---|---|
| `/api/auth` | register/login/refresh/logout, me, change/forgot/reset password |
| `/api/auth/email`, `/api/auth/phone` | OTP send + verify (SMTP / TextBee SMS) |
| `/api/verification` | Ghana Card verification: start → queued pipeline → status |
| `/api/profile` | profile CRUD, photo upload, digital ID card |
| `/api/settings`, `/api/communication-preferences` | notification prefs, password, account deletion |

**Listings & discovery**
| Route base | Purpose |
|---|---|
| `/api/properties` | CRUD, photos, `featured`, `search`, my-properties |
| `/api/properties/{id}` (availability) | available ranges, blocked dates |
| `/api/properties/{id}/walkthrough(s)` | video upload + admin/agent review queue — a listing needs an approved walkthrough to go live |
| `/api/properties/{id}/tour` | 360° room/hotspot tour JSON (public read) |
| `/api/search` | cross-entity search |
| `/api/wishlist` | saved properties |

**Booking & money**
| Route base | Purpose |
|---|---|
| `/api/bookings` | create (availability-checked), my-bookings, cancel + refund preview |
| `/api/escrow` | initiate Paystack payment, signed webhook, release/dispute/refund |
| `/api/receipts` | receipt list + PDF download |
| `/api/agreements` | rental agreement create/sign/PDF |
| `/api/pricing/{propertyId}` | weekend rate, monthly discount, cleaning fee |
| `/api/calendar` | per-day price/discount/blocked overlay for a month |
| `/api/statements` | landlord monthly revenue statements (10% mgmt fee) |
| `/api/payments/methods` | saved payment methods (masked, primary flag) |

**People & operations**
| Route base | Purpose |
|---|---|
| `/api/landlord` | workspace: stats, earnings, bookings/tenants/inquiries (paged) |
| `/api/personaldashboard` | per-role dashboards (tenant/landlord/agent/caretaker) |
| `/api/admin` | platform stats + audit logs |
| `/api/agents` | agent directory + viewing requests |
| `/api/caretakers` | caretaker directory, service requests, assignment, reviews |
| `/api/maintenance` | tenant tickets → convert to caretaker service request |
| `/api/tasks` | landlord operational task board (cleaning/maintenance/…) |
| `/api/team` | landlord team members (co-host/cleaner/…, invite/suspend) |
| `/api/inquiries` | pre-booking guest enquiries (guest-writable) |

**Community & content**
| Route base | Purpose |
|---|---|
| `/api/exchange` | owner forum: posts + replies (paged) |
| `/api/resources` | admin-curated guides/templates/videos |
| `/api/reviews` | property reviews (booking-gated) |
| `/api/trustscore` | reality score: verification + history + stay feedback |
| `/api/safety` | trusted contact, check-in, panic alert |
| `/api/chat` + `/hubs/chat` | conversations/messages REST + SignalR real-time |
| `/api/notifications` | in-app notifications, unread count |
| `/api/config` | public app info |

## 7. Key flows

**Ghana Card verification (the product's trust core)**
1. `POST /api/verification/start` (selfie + Ghana Card number) → request row
   `Pending`, job enqueued in `VerificationQueue` (in-proc channel), API
   returns immediately.
2. `VerificationProcessingService` (hosted) dequeues: `NiaClient` fetches the
   registered NIA record/photo from **TripNest.Id**; `FaceMatchClient` posts
   NIA photo URL + selfie (base64) to the **sidecar**, which returns
   similarity (Facenet512/RetinaFace) + liveness (MiniFASNet anti-spoofing on
   the selfie only).
3. Thresholds from config (`FaceMatchThreshold` 80, `LivenessThreshold` 75,
   max 5 attempts/hour) decide Approved/Rejected; user gets a notification;
   `[RequireVerified]` gates now pass. `GET /api/verification/status` polls.

**Booking + escrow payment**
1. `POST /api/bookings` validates dates + `AvailabilityService` range check;
   the DB exclusion constraint is the final arbiter against races.
2. `POST /api/escrow/initiate` → `PaystackPaymentGateway` creates the charge;
   money is held in escrow, not paid to the landlord.
3. Paystack calls `POST /api/escrow/webhook` — HMAC-SHA512 signature over the
   raw body verified in constant time; unverifiable calls are rejected.
4. Funds release on check-in + grace period (`EscrowAutoReleaseService` sweeps
   hourly, `Escrow:GracePeriodHours` = 24), or via explicit release; disputes
   freeze auto-release until an admin resolves; refunds go back through
   Paystack. Receipts are generated as QuestPDF documents.

**Real-time chat**: REST for history/CRUD (`/api/chat/*`), SignalR
(`/hubs/chat`) for live delivery; Redis backplane makes it multi-instance-safe.

**Notifications**: domain events → `NotificationService` persists in-app rows;
non-urgent SMS/email delivery goes through `NotificationDispatchQueue` →
`NotificationDispatchService` off the request thread.

## 8. Background services

| Hosted service | Job |
|---|---|
| `VerificationProcessingService` | drains the verification queue (NIA + face match) |
| `EscrowAutoReleaseService` | releases matured escrows past the grace period |
| `NotificationDispatchService` | sends queued SMS/email |
| `TrustScoreDailySnapshotService` | daily trust-score history snapshots |

Both queues are singleton in-proc channels — fine single-instance; a broker
would be needed for multi-instance fan-out.

## 9. Configuration & integrations

Secrets live in **user-secrets** (dev) / env vars (prod) — `appsettings.json`
holds only safe defaults. Notable keys: `ConnectionStrings:DefaultConnection`
(currently Azure Postgres), `Jwt:*`, `Services:TripNestId`,
`Services:FaceMatchSidecar`, `PaystackSettings:*`, `TextBeeSettings:*`,
`SmtpSettings:*`, `Redis:ConnectionString`, `Storage:Blob:*`,
`ApplicationInsights:ConnectionString`, `Verification:*`, `Escrow:*`,
`Cors:AllowedOrigins`, `Database:AutoMigrate`.

Everything degrades gracefully when unconfigured: no Redis → in-memory
(single-instance), no Blob → local `wwwroot/uploads`, no App Insights →
console-only telemetry, sidecars down → health reports Degraded but the API
stays ready (verification just can't complete).

## 10. Observability & health

- Serilog structured logs with trace ids; one summary line per request.
- OpenTelemetry traces/metrics (ASP.NET, HttpClient, Npgsql, runtime) exported
  to Azure Monitor when configured.
- `/health/live` (process up), `/health/ready` (Postgres gates; TripNest.Id +
  face-match reported but non-gating), `/health` (full report).

## 11. Testing

`TripNest.Core.Tests`: 182 xUnit integration tests booting the real app via
`WebApplicationFactory<Program>` with an in-memory EF provider per fixture.
`TestBase` gives `RegisterAndLoginAsync(role)` (returns user id + bearer),
`MarkUserVerifiedAsync`, `ClearAuth`. CI builds Release with `-warnaserror`.
Tests hit real HTTP endpoints — middleware, filters, auth and serialization
are all exercised, not mocked.
