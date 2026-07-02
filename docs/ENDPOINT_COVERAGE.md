# Endpoint Coverage Matrix (2026-07-02)

> Every backend endpoint → where the frontend (`Tripnest/Frontend`, branch
> `backend-integration`) uses it. "api module" = `src/api/*.ts`.
> Status: ✅ wired into UI · 🔌 client function exists, invoked situationally · 🚫 intentionally not in this app (see last section).

## Auth & account

| Endpoint | Used by |
|---|---|
| POST /auth/register, /auth/login, /auth/refresh-token, /auth/logout, GET /auth/me | ✅ `authStore` + WelcomePage; refresh runs automatically in `client.ts` |
| POST /auth/change-password → PUT /settings/password | ✅ AccountSettings (both settings pages) |
| POST /auth/forgot-password, /auth/reset-password | 🔌 `client` — no dedicated screen yet (WelcomePage "forgot password" link is the natural home) |
| POST /auth/email/send-otp, verify-otp; /auth/phone/send-otp, verify-otp | ✅ VerificationSection (ProfilePage) |
| POST /verification/start, GET /verification/status | ✅ VerificationSection (Ghana Card, with polling) |
| GET/PUT /profile/me, POST /profile/photo, GET /profile/id-card | ✅ ProfilePage (photo doubles as the verification selfie upload) |
| GET/PUT /settings/notifications, DELETE /settings/account | ✅ AccountSettings |
| GET/PUT /communication-preferences/mine | ✅ same data as /settings/notifications (that pair is the wired one) |

## Listings & discovery

| Endpoint | Used by |
|---|---|
| GET /properties, /properties/featured, /properties/{id} | ✅ Home/Search/PropertyDetail |
| GET /properties/search | ✅ SearchPage (debounced server search) |
| GET /properties/user/my-properties | ✅ landlord ListingsPage, pricing/calendar selectors |
| POST/PUT/DELETE /properties, POST /properties/{id}/photos | ✅ landlord ListingsPage (create form, rename, delete, photo upload) |
| POST /properties/{id}/walkthrough, GET .../walkthroughs | ✅ ListingsPage (video upload); status via listing badge |
| GET/PUT /properties/{id}/tour | ✅ VirtualTour (GET); PUT in `api/tours.ts` (owner editor UI pending design) |
| GET /properties/{id}/available-ranges, /availability; POST/DELETE blocked-dates | 🔌 `api/availability.ts` — calendar shows overlays; block/unblock buttons pending design |
| GET /search | 🔌 global search (top bar navigates to /search which uses /properties/search) |
| GET/POST/DELETE /wishlist | ✅ SavedPage + save toggle on PropertyDetail |
| GET /config/app-info | ✅ NearbyMap tile configuration |

## Booking & money

| Endpoint | Used by |
|---|---|
| POST /bookings, GET /bookings/user/my-bookings | ✅ CheckoutPage, BookingsPage, MyTrips |
| GET /bookings/{id}, cancellation-preview, POST cancel | ✅ BookingsPage Manage panel (preview shown before cancel) |
| POST /escrow/initiate, GET /escrow/{id} | ✅ CheckoutPage (Paystack redirect + polling) |
| GET /escrow/booking/{bookingId} *(new)* | ✅ BookingsPage dispute flow |
| POST /escrow/{id}/dispute | ✅ BookingsPage "Report a problem" |
| POST /escrow/{id}/release | 🔌 `api/payments.ts` — landlord release button pending design (auto-release covers the default path) |
| POST /escrow/{id}/refund, PATCH resolve-dispute | 🚫 admin actions |
| GET /receipts/mine, /receipts/booking/{id}, /receipts/{id}/download | ✅ PaymentsPage history + BookingsPage receipt PDF |
| POST /agreements, GET /agreements/mine, POST {id}/sign, GET {id}/download | ✅ AgreementsPage + BookingsPage Manage panel |
| GET/PUT /pricing/{propertyId} | ✅ PricingPage (per-listing selector) |
| GET /calendar | ✅ CalendarPage (per-listing selector) |
| GET /statements | ✅ StatementsPage |
| /payments/methods (GET/POST/PATCH/DELETE) | ✅ PaymentsPage + landlord payout card |

## Workspace, community, comms

| Endpoint | Used by |
|---|---|
| GET /landlord/stats, /earnings, /properties/performance | ✅ Overview, Earnings, ListingsPage stats |
| GET /landlord/bookings, /tenants, /inquiries (+status PATCH) | ✅ Reservations/Bookings/Tenants/Inquiries pages |
| POST /inquiries | ✅ PropertyDetail "Ask the host" box |
| GET /personaldashboard/tenant, /landlord | ✅ tenant HomePage, landlord Overview/Earnings |
| /tasks CRUD | ✅ TasksPage (list + done-toggle; create/delete in `api/hostTasks.ts`) |
| /team CRUD | ✅ UsersPage |
| /exchange posts + replies | ✅ OwnerExchangePage (threads load lazily, reply inline) |
| GET /resources | ✅ ResourcesPage (POST is admin-only) |
| /reviews: POST, property/{id}, mine, DELETE | ✅ post-stay review (BookingsPage), PropertyDetail reviews, landlord ReviewsPage |
| /trustscore property/user, stay-feedback | ✅ PropertyDetail badge + post-stay feedback |
| /chat conversations/messages/mark-read (+ SignalR hub) | ✅ MessagesPage (REST); live SignalR client not yet added (`@microsoft/signalr` install pending) |
| /notifications mine/read/mark-all/unread-count/DELETE | ✅ NotificationsPage + TopBar badges |
| /safety contact GET/PUT, checkin, alert | ✅ Settings trusted contact + HelpPage safety card |
| /maintenance POST/mine | ✅ MaintenancePage |
| GET /agents, /caretakers | ✅ ServiceDirectory |

## 🚫 Intentionally not wired — different roles/surfaces

These endpoints serve roles the frontend team's two-surface app (tenant +
landlord) doesn't render. They need their own portals (or an admin app):

- **Admin:** `/admin/stats`, `/admin/audit-logs`, walkthrough review queue
  (`/properties/pending-walkthroughs`, `.../walkthrough/review`), resource
  authoring (`POST /resources`), dispute resolution (`/escrow/{id}/resolve-dispute`),
  refunds (`/escrow/{id}/refund`).
- **Agent portal:** `/personaldashboard/agent`, `/agents/{id}/viewing-requests`,
  viewing-request status updates.
- **Caretaker portal:** `/personaldashboard/caretaker`, `/caretakers/service-requests/*`
  (accept/status/review), caretaker assignment, maintenance status updates
  (`PATCH /maintenance/{id}/status`, convert-to-service-request — landlord/caretaker actions).

Everything else in `API.md` is reachable from the UI or one call away in `src/api/`.
