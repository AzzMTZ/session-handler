# Session Handler

A backend service that ingests a stream of user **Login / Update / Logout** events across a
multi-tenant system and answers arbitrary queries over the resulting sessions — on any
attribute, any combination of attributes, and any point in time.

- **Stack:** C# / ASP.NET Core Web API (`net10.0`), EF Core + SQLite, xUnit.
- **Shape:** a layered HTTP API — `Controllers → Services → Repositories` — with the
  session as the primary resource and an immutable event log behind it.

---

## Contents

- [Quick start](#quick-start)
- [How to build, run and test](#how-to-build-run-and-test)
- [How events are handled](#how-events-are-handled)
- [How a consumer uses the API](#how-a-consumer-uses-the-api)
- [Approach and why](#approach-and-why)
- [Alternatives considered](#alternatives-considered)
- [Assumptions](#assumptions)
- [Trade-offs](#trade-offs)
- [What I'd do differently with more time](#what-id-do-differently-with-more-time)
- [Project layout](#project-layout)

---

## Quick start

```bash
# from the repository root
dotnet run --project SessionHandler/SessionHandler/SessionHandler.csproj
```

The API starts on `http://localhost:5284` (see `SessionHandler/SessionHandler/Properties/launchSettings.json`).
In the `Development` environment, interactive API docs are served at:

- **Swagger UI:** <http://localhost:5284/api>
- **OpenAPI document:** <http://localhost:5284/openapi/v1.json>

On startup the service applies EF Core migrations automatically and creates a local SQLite
file, `sessions.db`, in the project directory. Delete that file to reset all state.

---

## How to build, run and test

Everything is standard .NET tooling and runs the same on Windows, macOS and Linux. No
platform-specific dependencies.

### Prerequisites

- **.NET SDK 10.0** or later (`dotnet --version` should print `10.x`).

### Build

```bash
dotnet build SessionHandler/SessionHandler.sln
```

### Run

```bash
dotnet run --project SessionHandler/SessionHandler/SessionHandler.csproj
```

- Listens on `http://localhost:5284`.
- Creates / migrates `SessionHandler/SessionHandler/sessions.db` on first request-independent
  startup — no manual `dotnet ef database update` step.
- To start from a clean slate, stop the app and delete `sessions.db`.

### Test

```bash
dotnet test SessionHandler/SessionHandler.sln
```

The suite is almost entirely **black-box HTTP tests**: each test boots the real application
in-process with `WebApplicationFactory<Program>`, pointed at a private in-memory SQLite
database, and drives it only through HTTP requests — asserting on status codes and response
bodies, never on services or the `DbContext` directly. Coverage includes:

| Area | File |
| --- | --- |
| Session lifecycle + 400 / 404 / 409 responses | `SessionCrudTests` |
| Concurrent Login / Update for one identity | `SessionConcurrencyTests` |
| Event log written exactly once per successful call, atomically | `SessionEventInvariantTests` |
| `POST /sessions/search` — every filter, tag conjunction, active-only default, date ranges | `SessionSearchTests` |
| `POST /session-events/search` — scoping, type filter, time ranges | `SessionEventSearchTests` |
| `KeyedAsyncLock` and `DateTimeUtils` in isolation | `Unit/` |

---

## How events are handled

### Two records: `Session` and `SessionEvent`

| | Purpose | Mutability |
| --- | --- | --- |
| **`Session`** | Current, folded state of one connection — the thing consumers usually query | Updated in place as events arrive |
| **`SessionEvent`** | Append-only audit row for every accepted Login / Update / Logout | Never updated or deleted |

A user's **identity** is the pair `(tenantId, username)`. The same identity can be connected
from many IPs at once, so the natural key of an **active** session is the triple
`(tenantId, username, ip)`. Historical (logged-out) sessions are retained, so the `Sessions`
table can hold many rows for the same triple over time — an active session is one where
`logoutAt IS NULL`.

Every accepted event does **two writes in one transaction** (via a shared unit of work): the
session mutation *and* the corresponding `SessionEvent` row. A rejected call (409/404) writes
nothing.

### Event semantics

| Event | HTTP | Effect on the session | Rejected when |
| --- | --- | --- | --- |
| **Login** | `POST /sessions` | Opens a new active session for the triple with the given tags; records a `Login` event | A session for that triple is already active → **409 Conflict** |
| **Update** | `PUT /sessions/{tenantId}/{username}/{ip}` | Replaces the session's tag set and advances `lastSeenAt`; records an `Update` event | No active session for that triple → **404 Not Found** |
| **Logout** | `DELETE /sessions/{tenantId}/{username}/{ip}` | Sets `logoutAt`, closing the session; records a `Logout` event | No active session for that triple → **404 Not Found** |

- **Tags on Update are a full replacement**, not a merge — each Update carries the complete
  desired tag set for the session.
- **Logging in again after logout** opens a new, distinct session (new surrogate `id`); the
  previous one stays in the table as history.
- **Logout carries no tags.** The tag set at close is already on the preceding Login/Update
  events for anyone who needs it.

### Out-of-order events

Timestamps come from the caller and are trusted; ordering is driven by them, not the server
clock. Arrival order is not assumed to match timestamp order:

- **Update:** the event row is *always* recorded. Its tags/`lastSeenAt` are folded into the
  live session **only if** the event timestamp is at or after the session's current
  `lastSeenAt` — a stale Update never moves current state backwards.
- **Logout:** always closes the session, even if it arrives with an old timestamp — a
  terminal transition, so a session can't be left open forever. `lastSeenAt` still only ever
  advances.

### Concurrency safety

Concurrent events for the *same* identity triple are serialised so they can't race
(double-open on Login, lost update on Update). Two mechanisms, deliberately layered:

1. **`KeyedAsyncLock<(TenantId, Username, Ip)>`** — a process-wide singleton. Login, Update
   and Logout each hold the lock for their key across the whole *read → decide → write*.
   Different triples never block each other, so unrelated users and even different IPs of the
   same user run fully in parallel. Lock entries are ref-counted and dropped when
   uncontended, so the map stays bounded.
2. **Partial unique index** `IX_Sessions_ActiveIdentity` on `(TenantId, Username, Ip)`
   `WHERE LogoutAt IS NULL` — the database-level backstop. It guarantees "at most one active
   session per triple" even for a write that somehow bypasses the lock; a unique-violation on
   Login is translated to the same **409 Conflict**.

The lock is the hot path (clean errors, no exception-driven control flow); the index is the
correctness guarantee. Under 20 parallel `POST /sessions` for one triple the result is
exactly one `201 Created`, nineteen `409 Conflict`, one session row and one event row.

> Single-process only, which is within the assignment's stated scope (clustering /
> horizontal scaling explicitly out of scope). A distributed deployment would drop the
> in-process lock and lean on the database constraint plus row locking.

---

## How a consumer uses the API

All requests and responses are JSON with **camelCase** fields; enums serialise as strings
(`"Login"`, `"Update"`, `"Logout"`). Domain errors use
[RFC 7807 `ProblemDetails`](https://datatracker.ietf.org/doc/html/rfc7807).

### 1. Ingest events

**Login**

```http
POST /sessions
Content-Type: application/json

{
  "tenantId": "acme",
  "username": "alice",
  "ip": "10.0.0.1",
  "tags": ["role:admin", "team:blue"],
  "timestamp": "2026-08-28T09:00:00Z"
}
```

`201 Created`, `Location: /sessions/{id}`, body:

```json
{
  "id": 1,
  "tenantId": "acme",
  "username": "alice",
  "ip": "10.0.0.1",
  "tags": ["role:admin", "team:blue"],
  "loginAt": "2026-08-28T09:00:00Z",
  "lastSeenAt": "2026-08-28T09:00:00Z",
  "logoutAt": null
}
```

**Update** — identity triple in the path, mutable fields in the body:

```http
PUT /sessions/acme/alice/10.0.0.1
Content-Type: application/json

{
  "tags": ["role:admin", "team:green"],
  "timestamp": "2026-08-28T09:15:00Z"
}
```

`200 OK` with the updated session.

**Logout** — identity triple in the path, timestamp in the body:

```http
DELETE /sessions/acme/alice/10.0.0.1
Content-Type: application/json

{ "timestamp": "2026-08-28T17:30:00Z" }
```

`204 No Content`.

### 2. Query sessions — `POST /sessions/search`

The request body is a filter. **Every field is optional and filters are AND-combined.**
`tags` is a conjunction — a session must carry *all* listed tags. Results are ordered by
`loginAt` descending.

```jsonc
{
  "tenantId":   "acme",
  "username":   "alice",
  "ip":         "10.0.0.1",
  "tags":       ["team:blue"],
  "activeOnly": true,                                  // default: true
  "loginAt":    { "since": "2026-08-01T00:00:00Z", "until": "2026-08-31T23:59:59Z" },
  "logoutAt":   { "since": "...", "until": "..." },
  "lastSeenAt": { "since": "...", "until": "..." }
}
```

> **`activeOnly` defaults to `true`.** An empty body `{}` therefore returns only sessions
> that are still open. Pass `"activeOnly": false` to include historical (logged-out)
> sessions. This default is intentional: the overwhelmingly common question is "who is
> connected *now*", and it keeps the naive empty query cheap and safe.

> **Date ranges are inclusive.** In every `{ "since": ..., "until": ... }` block — `loginAt`,
> `logoutAt`, `lastSeenAt` here, and `timestamp` in the event search — `since` maps to `>=`
> and `until` to `<=`, so a bound that lands exactly on an event matches. Either end may be
> omitted. Strict (exclusive) `before` / `after` bounds are not exposed; if you need one,
> offset the value by the smallest representable amount (a `DateTime` tick, 100 ns).

Response: `SessionResponse[]` (same shape as the Login response above).

**The assignment's example questions map directly:**

| Question | Body |
| --- | --- |
| Which users are connected from tenant `X` on IP `Y`? | `{ "tenantId": "X", "ip": "Y" }` |
| Which IPs is user `U` in tenant `T` connected from? | `{ "tenantId": "T", "username": "U" }` → each result's `ip` |
| Among active sessions, which were logged in at/after 3pm yesterday? | `{ "loginAt": { "since": "2026-08-27T15:00:00Z" } }` |

**Sessions that were active at an instant `T`** — including ones that have since logged
out — is the one point-in-time question a single filter body can't express. It needs
`loginAt <= T AND (logoutAt IS NULL OR logoutAt >= T)`, but the `logoutAt` range drops
still-open sessions. Today this is **two requests, unioned by the caller**:

```jsonc
// A — still open, started on or before T
{ "activeOnly": true,  "loginAt": { "until": "2026-08-27T15:00:00Z" } }

// B — started on or before T, closed on or after T
{ "activeOnly": false,
  "loginAt":  { "until": "2026-08-27T15:00:00Z" },
  "logoutAt": { "since": "2026-08-27T15:00:00Z" } }
```

The two result sets are disjoint — A is `logoutAt: null`, B is not — so the caller
concatenates them (dedupe by `id` if a Logout may land between the two reads). This returns
the correct *set* of sessions, but each carries its final tags, not its tags as of `T`.
Single-call alternatives (`activeAt`, an `allowNull` range flag) and the fuller
tags-as-of-`T` reconstruction are covered under
[Alternatives considered](#alternatives-considered) and
[What I'd do differently](#what-id-do-differently-with-more-time).

### 3. Query the raw event log — `POST /session-events/search`

Same conventions. Use this for history and finer-grained point-in-time questions (e.g. every
tag change on a session, or every Login in a window).

```jsonc
{
  "sessionId": 1,                 // scope to one exact session instance
  "tenantId":  "acme",
  "username":  "alice",
  "ip":        "10.0.0.1",
  "tags":      ["team:blue"],
  "type":      "Update",          // "Login" | "Update" | "Logout"
  "timestamp": { "since": "...", "until": "..." }
}
```

Response: `SessionEventResponse[]` — `id`, `sessionId`, the identity triple, `tags`
(null for Logout), `timestamp`, `type` — ordered by `timestamp` descending. An empty body
returns every event ever recorded.

### 4. Fetch a single record by id

- `GET /sessions/{id}` → `200` / `404`. By surrogate id because the identity triple is
  unambiguous only for the *active* session, and a GET isn't restricted to that.
- `GET /session-events/{id}` → `200` / `404`.

### Error model

| Status | Meaning |
| --- | --- |
| `400 Bad Request` | Malformed / missing required JSON — `ValidationProblemDetails` |
| `404 Not Found` | Update/Logout with no active session; unknown id |
| `409 Conflict` | Login while a session for that triple is already active |

---

## Approach and why

**REST with the session as the resource, not a generic event endpoint.**
The lifecycle maps cleanly onto HTTP: Login *creates* a resource (`POST` → `201` +
`Location`), Update *mutates* it (`PUT` → `200`), Logout *ends* it (`DELETE` → `204`). The
underlying events are still first-class and fully queryable at `/session-events`, so nothing
is lost — but consumers get standard verbs, status codes and an OpenAPI contract instead of a
custom typed-envelope protocol they'd have to learn. It's also the fastest path to something
correct and well-tested in the time available.

**Layered architecture:** `Controller` (HTTP) → `Service` (domain rules, transactions,
locking) → `Repository` (EF Core data access), wired through interfaces. A single
`IUnitOfWork` is the only commit point, which is what lets a session mutation and its event
row land atomically.

**Search as `POST /…/search` with a JSON filter body.** The requirement is "any attribute or
combination, including time". Tag arrays and nested `since`/`until` ranges on multiple fields
don't express cleanly or unambiguously as query-string parameters. A typed request record is
self-documenting via OpenAPI and extends without breaking callers — a new filter is just a
new optional property. The accepted cost is that these reads are `POST`, so they're not
HTTP-cacheable and aren't a "pure" GET.

**EF Core + SQLite.** Zero external setup, one file, runs identically on every platform, and
the schema self-migrates on startup. Since distributed deployment is out of scope, a
single-file embedded database is enough to demonstrate the model and the query surface. The
data-access code is behind repository interfaces and uses no SQLite-specific features beyond
a partial index, so swapping in PostgreSQL is a provider change, not a rewrite.

**Point-in-time, today:** the session table gives an efficient **"as of its last update"**
view of every session, historical ones included — the main use case. "Which sessions were
open at instant `T`" is a union of two `/sessions/search` calls (shown under
[How a consumer uses the API](#how-a-consumer-uses-the-api)); anything finer is answered by
querying the immutable event log directly. Full "rebuild each session as it was at instant
`T`" as a single call is not yet implemented (see
[What I'd do differently](#what-id-do-differently-with-more-time)).

---

## Alternatives considered

| Option | Decision | Why |
| --- | --- | --- |
| **GraphQL** for the query surface | Rejected | A genuinely good fit for "query any combination of fields", but the schema, resolver and tooling overhead wasn't worth it at this scope. `POST /search` with a typed filter covers the same need. |
| **Single-call "sessions active at `T`"** — an `activeAt: T` field, or an `allowNull` flag on the `logoutAt` range | Deferred | Both fold the [two-request union](#2-query-sessions--post-sessionssearch) into one call. `activeAt` is the better of the two: one self-describing field that supersedes the `activeOnly` default. `allowNull` is a modifier that's only meaningful on the single nullable date field and reads as nonsense with an `until` bound. Neither earns its surface area for a query the brief's examples don't ask for, and `activeAt` still answers only *which* sessions, not their state at `T`. The two-request recipe covers the need today. |
| **PostgreSQL** | Rejected for now | The right production choice, but it needs a running server and buys nothing while clustering / multi-instance is out of scope. Kept the persistence layer provider-agnostic so this is an easy switch. |
| **Redis cache** for the active-session set | Rejected | Adds an external dependency and cache-coherency logic for a single-process service. Not justified at this scale. |
| **In-memory cache** for the active-session set | Rejected | Every process restart would need to replay the event log to rebuild current state before the service could answer queries — a cold-start availability gap. Reading active sessions straight from the indexed table avoids it. |
| **Async / queued ingestion** (broker + worker) | Deferred | Synchronous processing gives the caller immediate, meaningful feedback (`409` on a duplicate Login, `404` on an orphan Update) that fire-and-forget would lose, and is sufficient at the assumed event rate. The clear next step if throughput demands it. |

---

## Assumptions

- **Caller-supplied timestamps are authoritative.** Event ordering, `lastSeenAt` and
  point-in-time filters are all driven by the `timestamp` in the payload, not the server
  clock. Events may arrive out of timestamp order.
- **At most one active session per `(tenantId, username, ip)`.** A second Login for a triple
  that's already active is a conflict, not a silent no-op or a re-open.
- **Update sends the full tag set.** Tags are replaced wholesale; there is no add/remove
  delta operation.
- **Logout is terminal and unconditional.** It always closes the active session, even when it
  arrives late. A Logout for a triple with no active session is a `404`, not a no-op.
- **Identifiers are opaque strings.** No validation of `ip` as a well-formed address, no
  tenant/username format or existence checks (authn/authz of the caller is explicitly out of
  scope).
- **Single instance.** No coordination across processes is attempted; the in-process lock
  assumes one writer process.
- **Unbounded retention.** Sessions and events are kept forever — no archival or pruning.

## Trade-offs

- **`POST` for search** — expressive and OpenAPI-typed, but gives up HTTP caching and GET
  semantics.
- **In-process lock for concurrency** — simple and fast, correct only for a single process.
  The DB unique index is the portable backstop, but on its own it would force
  exception-driven control flow on the write path.
- **State folded into the `Sessions` table** — session queries stay simple and fast, but true
  "state as of arbitrary T" needs the event log and isn't a first-class query yet.
- **`DELETE` with a request body** for the Logout timestamp — keeps the identity triple in
  the URL and the payload symmetric with Update, but some intermediaries and clients are
  unfriendly to bodies on `DELETE`. A `?timestamp=` query parameter is the fallback.
- **Auto-migrate on startup** — frictionless for a single instance and for this review; a
  multi-instance deployment would run migrations as a separate, gated step.
- **Minimal input validation** — keeps the surface small for the exercise; a real deployment
  needs stricter checks (IP format, non-empty identifiers, sane timestamps).
- **No pagination on search** — fine for demonstration data, not for production result sets.
- **SQLite** — portability and zero-setup now, at the cost of write concurrency and features
  a server database would give.

## What I'd do differently with more time

1. **First-class point-in-time session queries.** Two levels. The cheap one: an `activeAt: T`
   field that returns the *set* of sessions open at `T` in a single call, replacing the
   current [two-request union](#2-query-sessions--post-sessionssearch). The fuller one:
   reconstruct sessions *as they were* at `T` — query the `SessionEvents` table joined to
   `Sessions` for each session's last event at or before `T`, project a `Session` (tags
   included) from each, and return that list. This makes "who was connected, and with which
   tags, at 3pm yesterday" a single call instead of a manual event-log assembly.
2. **Queue-based ingestion** — a broker plus a worker to decouple accept from process and
   absorb bursts, once throughput justifies it.
3. **Bulk ingestion endpoint** — accept a batch of events in one request.
4. **Pagination / cursors** on both search endpoints.
5. **Stricter validation** — IP well-formedness, non-empty identifiers, rejection or clamping
   of implausible timestamps; structured `ValidationProblemDetails` per field.
6. **PostgreSQL** provider with real migration/deploy steps, plus load testing of the query
   paths and indexes.
7. **Observability** — structured request logging, metrics (ingest rate, conflict rate, query
   latency), tracing.
8. **Retention / archival** policy for old sessions and events.
9. **Richer concurrency story for multi-instance** — drop the in-process lock, rely on the
   DB constraint plus explicit row locking / upserts.
10. **A GraphQL read endpoint** alongside `/search` if consumers want field-level selection.

---

## Project layout

```
SessionHandler/
  SessionHandler.sln
  SessionHandler/                     # the API
    Controllers/                      # HTTP surface — Sessions, SessionEvents
    Services/                         # domain rules, transactions, per-identity locking
    Repositories/                     # EF Core data access + SessionDbContext + UnitOfWork
    Interfaces/                       # ports between the layers
    Models/                           # Session, SessionEvent, SessionEventType
    Dtos/                             # request/response records, query filters, DateRange
    Exceptions/                       # domain exceptions + RFC 7807 mapping
    Utils/                            # KeyedAsyncLock, DateTimeUtils
    Migrations/                       # EF Core migrations (applied on startup)
  SessionHandler.Tests/              # xUnit — HTTP-level e2e + unit tests
System Design.excalidraw             # requirements, data model, endpoint and flow sketch
```
