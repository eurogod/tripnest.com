# TripNest.Core — Test & Run Instructions

How to build, run, and test the API, its sidecars, and the AI features. Nothing
below requires paid credentials — the app and its whole test suite run fully
offline; the sidecars and AI keys are only needed to exercise those specific
flows end-to-end.

- **API base URL (local):** `http://localhost:5091`
- **Swagger (Development):** `http://localhost:5091/swagger`
- **Health:** `GET /health/live`, `GET /health/ready`, `GET /health`

---

## 1. Prerequisites

| Tool | Needed for | Notes |
|---|---|---|
| **.NET 8 SDK** | API + tests | `dotnet --version` should print `8.x` |
| **PostgreSQL 14+** | running the API | Default conn: `Host=localhost;Port=5432;Database=tripnest_core;Username=postgres;Password=root` (override in user-secrets) |
| **Python 3.11** | face-match sidecar only | tensorflow 2.16 needs Python ≤ 3.12 — 3.11 is safest |
| **Anthropic or Gemini API key** | AI features only | Optional — AI degrades gracefully to a friendly 400 when absent |

> The test suite uses the **EF in-memory provider** and **stubs every external
> dependency** (SMS, email, Paystack, and the AI client). You do **not** need
> Postgres, Python, or any API key to run `dotnet test`.

---

## 2. Run the test suite (no setup required)

From the repo root:

```bash
# Full suite — what you'll run most of the time
dotnet test --filter "FullyQualifiedName!~Live"

# What CI runs (Release, warnings are errors)
dotnet build --configuration Release -warnaserror
dotnet test --filter "FullyQualifiedName!~Live"

# A single class
dotnet test --filter "FullyQualifiedName~AssistantTests"

# A single test
dotnet test --filter "FullyQualifiedName~AllEndpointsSmokeTests.GenerateListingCopy_UnverifiedCaller_ShouldReturnForbidden"
```

Current suite: **290 tests**, all passing. Coverage includes the auth contract
of **every** endpoint (`AllEndpointsSmokeTests` — anonymous → 401, wrong role /
unverified → 403), plus happy-path lifecycle tests per module.

The AI features are tested with a controllable stub (`StubAiClient`), so
assistant/listing-copy/chat-suggestion behaviour is verified without any real
model calls.

---

## 3. Run the API

```bash
# 1. Point the connection string at your Postgres (once)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=tripnest_core;Username=postgres;Password=<your-pw>" \
  --project TripNest.Core

# 2. Run — migrations auto-apply on startup in Development, and demo accounts are seeded
dotnet run --project TripNest.Core
```

Then open `http://localhost:5091/swagger`.

- **Migrations** apply automatically in Development (`Database:AutoMigrate`,
  defaults to `IsDevelopment()`). In production, apply out-of-band with
  `dotnet ef database update --project TripNest.Core` and set `AutoMigrate=false`.
- Core runs **standalone** — the two sidecars (§6, §7) are only needed for the
  identity-verification flow, and the AI keys (§5) only for AI features.

### Seeded demo accounts (Development only)

| Role | Email | Password |
|---|---|---|
| Admin | `admin@tripnest.local` | `Admin@123456` |
| Landlord | `kwame@tripnest.local` | `Landlord@123456` |
| Tenant | `kofi@tripnest.local` | `Tenant@123456` |
| Agent | `ekow@tripnest.local` | `Agent@123456` |
| Caretaker | `ebo@tripnest.local` | `Caretaker@123456` |

---

## 4. Exercise the API manually

Get a token, then send it as `Authorization: Bearer <token>`.

```bash
# Log in (returns { data: { accessToken, userId, ... } })
TOKEN=$(curl -s http://localhost:5091/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"kofi@tripnest.local","password":"Tenant@123456"}' \
  | jq -r '.data.accessToken')

# Call a protected route
curl -s http://localhost:5091/api/auth/me -H "Authorization: Bearer $TOKEN" | jq

# Public browse (no token)
curl -s http://localhost:5091/api/properties | jq
curl -s "http://localhost:5091/api/agents?serviceArea=Accra&page=1&pageSize=20" | jq
curl -s "http://localhost:5091/api/caretakers?area=East+Legon" | jq
```

See `API.md` for the full endpoint reference (auth level, roles, verification
gates are all marked).

---

## 5. AI features (assistant, listing copy, chat suggestions, scam detection)

All AI runs behind the `IAiClient` seam and **degrades gracefully**: with no key
configured, AI endpoints return a clear **400** ("AI not configured") instead of
failing — exactly like the SMS/email/Paystack integrations. Pick **one** provider:

```bash
# Option A — Claude (best quality, paid). Default provider.
dotnet user-secrets set "Ai:ApiKey" "sk-ant-..." --project TripNest.Core
# Model defaults to claude-opus-4-8; override with Ai:Model if desired.

# Option B — Google Gemini (free AI Studio tier, no card needed)
dotnet user-secrets set "Ai:Provider" "gemini" --project TripNest.Core
dotnet user-secrets set "Ai:Gemini:ApiKey" "AIza..." --project TripNest.Core
```

AI endpoints once a key is set:

| Endpoint | Auth | What it does |
|---|---|---|
| `POST /api/properties/{id}/generate-copy` | 🔒 🛡️ owner | Drafts `{title, description, highlights}` from facts + photos (review-only, never auto-applied) |
| `POST /api/assistant/ask` | 🔒 | Grounded Q&A; escalates to an admin support ticket when a human is needed |
| `GET /api/assistant/history` | 🔒 | The caller's assistant conversation |
| `POST /api/chat/conversations/{id}/suggest-reply` | 🔒 | Drafts a reply for a chat participant to edit and send |
| `GET /api/admin/support-tickets` | 🔒 `[Admin]` | Open assistant escalations |
| `POST /api/admin/support-tickets/{id}/resolve` | 🔒 `[Admin]` | Close a ticket |

Scam detection runs automatically in the background on new chat messages (no
endpoint) via `ScamDetectionService`, wired into `ChatService`.

Quick check with a token:

```bash
curl -s http://localhost:5091/api/assistant/ask \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"question":"How does escrow work?"}' | jq
```

---

## 6. Face-match sidecar (identity verification)

Python/DeepFace service on `:5001`, called by `FaceMatchClient` during Ghana
Card verification. Needs **Python 3.11** + tensorflow/DeepFace (large first
install; model weights download on first request).

```bash
cd TripNest.Core/FaceMatchService
./run.sh          # creates the venv + installs deps on first run, then serves on :5001
# or manually:
python3.11 -m venv venv && source venv/bin/activate
pip install -r requirements.txt
pip install torch==2.2.2 --index-url https://download.pytorch.org/whl/cpu   # CPU-only wheel (liveness model)
uvicorn main:app --host 0.0.0.0 --port 5001
```

- Interactive docs: `http://localhost:5001/docs`
- Sidecar tests: `pip install -r requirements-dev.txt && pytest`
- Core points at it via `Services:FaceMatchSidecar` (default `http://localhost:5001`).

---

## 7. TripNest.Id sidecar (Ghana Card registry)

A **separate service/repository** (not in this repo) that Core calls over HTTP
for the Ghana Card lookup — Core never touches its database directly. Run it from
its own repo on `:5135` (Core points at it via `Services:TripNestId`, default
`http://localhost:5135`). Only needed for the identity-verification flow.

The identity flow is async: `POST /api/verification/start` queues the request
(returns `Pending`), a background worker calls TripNest.Id + the face-match
sidecar, and the client polls `GET /api/verification/status` for
`Verified`/`Rejected`.

---

## 8. Live integration tests (opt-in, hits real services)

Skipped by default. To run them you need real TextBee/SMTP/Paystack keys and
recipient config in user-secrets (`LiveTest:Phones`, `LiveTest:Emails`):

```bash
RUN_LIVE_INTEGRATION=1 dotnet test --filter "FullyQualifiedName~Live"
```

---

## Troubleshooting

- **DB connection timeout on startup / tests hang against a real DB** — the API
  targets whatever `DefaultConnection` points at. If that's a remote (e.g.
  Azure) Postgres and your IP changed, re-add your IP to the server firewall.
- **`python3.11 not found`** — the sidecar needs Python ≤ 3.12; install 3.11 or
  set `PYTHON=python3.12 ./run.sh`.
- **AI endpoint returns 400 "not configured"** — expected when no `Ai:ApiKey` /
  `Ai:Gemini:ApiKey` is set; it's a graceful no-op, not a bug.
- **Build warning fails CI** — CI builds Release with `-warnaserror`; keep the
  build warning-clean (`dotnet build --configuration Release -warnaserror`).
