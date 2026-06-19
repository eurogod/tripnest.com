# TripNest.Core — API Reference

.NET 8 Web API backend for an accommodation-booking platform centred on trust,
identity verification, and escrow-protected payments (Ghana-oriented).

- **Base URL (local):** `http://localhost:5091`
- **Interactive docs:** `http://localhost:5091/swagger` (Development only)
- **Health check:** `GET /health`
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
| POST | `/refresh-token` | 🌐 |
| POST | `/forgot-password` | 🌐 |
| POST | `/reset-password` | 🌐 |
| GET | `/me` | 🔒 |
| POST | `/change-password` | 🔒 |

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
| POST | `/` | 🔒 🛡️ |
| PUT | `/{propertyId}` | 🔒 🛡️ |
| DELETE | `/{propertyId}` | 🔒 🛡️ |

### Availability — `api/properties/{propertyId}`
| Method | Path | Access |
|---|---|---|
| GET | `/availability` | 🌐 |
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
| GET | `/{bookingId}` | 🌐 |
| POST | `/` | 🔒 |
| GET | `/user/my-bookings` | 🔒 |
| POST | `/{bookingId}/cancel` | 🔒 |

### Escrow — `api/escrow`
| Method | Path | Access |
|---|---|---|
| POST | `/initiate` | 🔒 |
| POST | `/webhook` | 🌐 (HMAC-signed; unsigned → 401) |
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
| PUT | `/password` | 🔒 |
| DELETE | `/account` | 🔒 |

### Safety — `api/safety`
| Method | Path | Access |
|---|---|---|
| POST | `/checkin` | 🔒 |
| POST | `/alert` | 🔒 |

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

---

## Real-time (SignalR)

- **Hub:** `/hubs/chat` (requires JWT). Browser clients pass the token via the
  `access_token` query string on the WebSocket handshake.
- **Server → client events:** `ReceiveMessage`, `UserTyping`, `UserStoppedTyping`.
- **Client → server methods:** `Typing`, `StopTyping` (broadcast to the other participant).
- REST `POST /api/chat/conversations/{id}/messages` also broadcasts live, so non-realtime
  clients and connected clients stay in sync.
