# NovaWallet Ledger Service

A wallet ledger for the NovaWallet module of FirstBank NovaPay, built in C# on .NET 8.

This is the component that must never lose, duplicate or miscount a customer's money. The design decisions below are all in service of that one requirement.

---

## Quick start

```bash
docker compose up --build
```

That single command builds the API image, starts PostgreSQL, waits for it to become healthy, applies database migrations, and starts the service.

| What | Where |
|---|---|
| Swagger UI | http://localhost:8081/swagger |
| Liveness | http://localhost:8081/health/live |
| Readiness | http://localhost:8081/health/ready |
| PostgreSQL (from host) | `localhost:5433` |

**Why non-default ports.** The API is exposed on host port 8081 and PostgreSQL on 5433, because 8080 and 5432 are frequently occupied on development machines — by corporate web proxies and local PostgreSQL installs respectively. Both are single-line changes in `.env` if your machine differs. Inside the Compose network the services still use their standard ports.

### Trying it out

1. `POST /api/auth/token` with `{"customerId": "alice"}` and copy the `accessToken`.
2. Click **Authorize** in Swagger and paste the token.
3. `POST /api/wallets` to open a wallet.
4. `POST /api/wallets/{id}/credit` with `{"amountKobo": 100000}` to fund it with ₦1,000.
5. Repeat steps 1–3 as `"bob"` to create a second wallet, then switch back to Alice's token.
6. `POST /api/wallets/{id}/transfers` with an `Idempotency-Key` header and `{"destinationWalletId": "...", "amountKobo": 40000}`.
7. **Send the identical request again.** It returns the same reference and the balance does not move a second time.
8. `GET /api/wallets/{id}/statement` to see the history, newest first.

### Inspecting the audit trail

The audit log is a separate table from the customer-facing ledger and can be queried directly:

```bash
docker compose exec db psql -U novawallet -d novawallet -c "
  SELECT event_type, amount_kobo, balance_before_kobo, balance_after_kobo,
         actor, correlation_id, reference, occurred_at_utc
  FROM audit_logs
  ORDER BY occurred_at_utc DESC;"
```

The `correlation_id` column matches the `X-Correlation-Id` response header, so any audit row can be traced to the exact HTTP request that produced it.

Try to tamper with it:

```bash
docker compose exec db psql -U novawallet -d novawallet -c "DELETE FROM audit_logs;"
```

The database refuses.

---

## Running the tests

```bash
dotnet test
```

Integration tests start a real PostgreSQL 16 container via Testcontainers, so Docker must be running.

| Project | What it covers |
|---|---|
| `NovaWallet.Domain.Tests` | Money arithmetic, overflow, wallet invariants, lock-ordering determinism |
| `NovaWallet.Application.Tests` | Transfer orchestration with mocked dependencies: replay, conflict, in-progress keys, key release rules |
| `NovaWallet.IntegrationTests` | Real HTTP pipeline, real JWTs, real database: end-to-end flow, authorisation, idempotency, daily limit, concurrency |

### The concurrency tests

`ConcurrencyTests` holds 50 transfer requests at a gate and releases them simultaneously against a wallet funded for exactly 25 of them. It asserts, exactly rather than approximately:

- the balance never goes negative;
- successful transfers × amount equals both the source's decrease and the destination's increase, so no money is created or destroyed;
- no more than 25 transfers succeed;
- the ledger, summed independently, reconstructs the final balance exactly;
- every ledger entry has a matching audit entry.

A second test fires 20 transfers each way between two wallets at once — the interleaving that deadlocks without a fixed lock order — and asserts no request fails.

**Verifying the test can fail.** A concurrency test that passes proves little on its own. Removing `FOR UPDATE` from `TransferExecutor.LockWalletAsync` makes these tests fail; restoring it makes them pass. The suite was also run ten consecutive times to check for flakiness.

---

## Architecture

Clean Architecture, four layers, with dependencies pointing inward.

```
┌──────────────────────────────────────────────────────────┐
│ NovaWallet.Api                                           │
│   Controllers · middleware pipeline · Swagger · DI root  │
├──────────────────────────────────────────────────────────┤
│ NovaWallet.Infrastructure                                │
│   EF Core + PostgreSQL · transfer executor · idempotency │
│   store · JWT · health checks                            │
├──────────────────────────────────────────────────────────┤
│ NovaWallet.Application                                   │
│   MediatR use cases · DTOs · FluentValidation · contracts│
├──────────────────────────────────────────────────────────┤
│ NovaWallet.Domain                                        │
│   Money · Wallet · LedgerEntry · AuditLogEntry           │
│   Zero external dependencies                             │
└──────────────────────────────────────────────────────────┘
```

The Domain project references no NuGet packages at all, so the money rules can be unit tested without a database or a web host. The Api project references Infrastructure only so that `Program.cs` can compose the dependency graph; no controller touches an Infrastructure type.

---

## Key decisions

### Money is an integer count of kobo

Every monetary value is a `long` number of kobo, wrapped in a `Money` value object. There is no `float`, `double` or `decimal` in any calculation.

`decimal` is not wrong the way `double` is — it has no binary rounding error. Integer minor units were chosen because they are stricter:

- **Illegal states are unrepresentable.** There is no denomination below the kobo, so a type that cannot hold ₦100.505 cannot create one.
- **Rounding is forced into the open.** Integer division truncates visibly, so any split must handle its remainder explicitly rather than silently at 28 decimal places.
- **The column has no scale to get wrong.** `bigint` maps one way. A mismatch between an EF mapping and a `numeric(18,2)` column is a common source of silent truncation.
- **It matches the rails.** NIBSS NIP and most payment APIs express amounts in minor units.

`Money` also refuses to be negative, uses `checked` arithmetic so overflow throws rather than wrapping, and exposes `ToNaira()` strictly for display. That method is the only place `decimal` appears in the codebase.

Every amount crossing the API is named `amountKobo`, never a bare `amount`, so a client cannot mistake the unit.

### Transfers are concurrency-safe by construction

A transfer runs as a single database transaction that:

1. locks both wallet rows with `SELECT … FOR UPDATE`;
2. **always locks in ascending wallet-id order**, never source-then-destination, so two opposing transfers queue behind each other instead of each holding what the other needs;
3. re-reads both balances after the locks are held, treating any earlier read as stale;
4. checks and increments the daily limit;
5. applies the debit and credit, writes both ledger entries and both audit entries;
6. commits all of it or none of it.

A `CHECK (balance_kobo >= 0)` constraint is the final backstop: if every line of application code were wrong, the database would still refuse to store a negative balance.

The daily-limit check is deliberately **inside** this transaction rather than in the handler. Checked outside, two concurrent transfers could both pass it and together exceed the limit. Inside, every transfer already holds an exclusive lock on its source wallet, so the limit row is serialised transitively.

Transfers are not modelled as a single domain aggregate. Each `Wallet` enforces its own invariants; the atomicity of the pair is a persistence concern, owned by `ITransferExecutor`.

### Idempotency: insert first, never check-then-insert

The transfer endpoint requires an `Idempotency-Key` header. The key is the primary key of the `idempotency_records` table.

The store **inserts first** and lets the primary-key violation reveal a duplicate. The obvious alternative — look for the key, insert if absent — has a race window in which two concurrent replays both see nothing and both proceed.

A claimed key resolves to one of four outcomes:

| Situation | Response |
|---|---|
| New key | Process the transfer, store the response |
| Same key, same payload, completed | Replay the stored response; no money moves |
| Same key, different payload | `409 Conflict` |
| Same key, same payload, still running | `409 Conflict`; the client should retry shortly |

The payload is fingerprinted with SHA-256 over the deserialised request, so reordering JSON fields does not count as a different request.

**Releasing keys on failure.** A key is released only for failures that provably happen before money moves: wallet not found, access denied, insufficient funds, daily limit exceeded, invalid amount. Any other failure leaves the key claimed, because it may have occurred after the transfer committed. A stuck key is recoverable by support; a double debit is not. A background job clears claims abandoned for more than five minutes by a crashed request.

### Daily limit and West Africa Time

The limit is ₦500,000 of outbound transfers per wallet per day, resetting at midnight WAT. It is a domain constant rather than configuration, because it is a business rule a client must never influence.

WAT is computed as a fixed UTC+1 offset rather than through `TimeZoneInfo`. WAT has no daylight saving, and time-zone identifiers differ between Windows (`W. Central Africa Standard Time`) and Linux (`Africa/Lagos`). A lookup that works on a Windows development machine would fail inside the Linux container.

### The audit trail is append-only at three layers

Every balance mutation writes an `AuditLogEntry` recording the amount, the balance before and after, the actor, the correlation id and the reference, in the same transaction as the mutation.

Immutability is enforced structurally rather than by convention:

1. **Domain.** `AuditLogEntry` has no public constructor, no setters, and no method that changes an instance.
2. **Persistence.** `NovaWalletDbContext.SaveChangesAsync` throws if any audit entry is tracked as modified or deleted.
3. **Database.** Triggers reject `UPDATE`, `DELETE` and `TRUNCATE` on `audit_logs`, whatever role connects.

There is deliberately no endpoint to read the audit log. An audit trail with a read API tends to acquire a write API.

### Errors are RFC 7807 Problem Details

One middleware converts every exception into `application/problem+json`. Controllers contain no try/catch.

| Failure | Status |
|---|---|
| Validation failure, invalid amount, self-transfer | 400 |
| Wallet belongs to another customer | 403 |
| Wallet not found | 404 |
| Idempotency conflict or key in progress | 409 |
| Insufficient funds, daily limit exceeded | 422 |
| Rate limit exceeded | 429 |
| Anything unexpected | 500 |

400 and 422 are distinguished deliberately: 400 means the request was malformed; 422 means it was well-formed but the account's current state prevents it. Unknown failures return only a generic message and the correlation id — internal detail never crosses the wire.

### Middleware order

```
Correlation ID          outermost, so even a 500 carries a trace id
Exception handling      wraps everything that can throw
Serilog request logging inside error handling, so failures still log a status
Authentication
Rate limiting           after authentication, so it can partition per customer
Authorization
Controllers
```

Rate limiting sits after authentication by choice. Placed earlier it would be cheaper under attack, but it could only partition by IP address — and many real customers share one IP behind a mobile carrier's NAT.

### Authentication

JWT bearer tokens, validated with a pinned algorithm (HS256 only, blocking `alg: none` and algorithm-confusion attacks), zero clock skew, and issuer, audience and lifetime checks. The service refuses to start without a signing key of at least 32 bytes.

**`/api/auth/token` is a mock issuer, as the brief permits.** It mints a token for any customer id with no credentials. 

### Rate limiting

- **Transfers:** a token bucket per customer, 10 tokens, replenishing 5 every 10 seconds. A token bucket allows a legitimate burst while capping the sustained rate; a fixed window would permit double the limit across a boundary.
- **Token issuance:** 10 per minute per IP. It is unauthenticated and mints credentials, so it is the most obvious abuse target in the service.

Rejections return Problem Details with a `Retry-After` header.

### Logging

Serilog, writing compact JSON to stdout, which is what a container orchestrator collects. Every line carries the correlation id.

What is deliberately **not** logged: the `Authorization` header, idempotency keys, and request bodies. EF Core's SQL logging is raised to Warning, because at Information it records parameter values — wallet ids and amounts. Under the Nigeria Data Protection Act 2023, customer financial data does not belong in a log aggregator.

### Health checks

- `/health/live` checks nothing external. If it depended on the database, a database outage would cause the orchestrator to restart every API instance, producing a crash loop on top of the original failure.
- `/health/ready` checks PostgreSQL with a three-second timeout. An instance that loses its database is removed from the load balancer without being killed, and returns automatically on recovery.

Failure responses omit exception messages, which can expose database hostnames on an unauthenticated endpoint.

---

## Assumptions

Where the brief was silent, these assumptions were made:

- **One wallet per customer is not enforced.** A customer may open several wallets.
- **The credit endpoint simulates inbound NIP.** It performs no ownership check, because inbound money comes from a third party. See the limitation below.
- **Access to another customer's wallet returns 403, not 404.** This is clearer for the demonstration, at the cost of confirming the wallet exists. A production service would likely return 404 for both.
- **A single transfer may not exceed the daily limit.** Such a request is rejected at validation, before any lock is taken.

---

## Known limitations and trade-offs

Stated plainly rather than left for a reviewer to find.

**Any authenticated customer can credit any wallet.** This is intentional for the simulation, but as built it is a money-minting endpoint. In production it would sit on a separate route secured by a partner API key or a signed NIBSS webhook, not by a customer's token.

**Rate limits are per process.** Behind a load balancer, each instance enforces its own limit, so the effective limit is the configured value times the instance count. Fleet-wide enforcement needs shared state such as Redis, or a gateway-level limiter.

**No database retry policy.** EF Core's retrying execution strategy conflicts with manually managed transactions, and for the money path a failure that surfaces is preferable to a retry that might re-run a partially applied transfer.

**Migrations run at startup.** Required for the single-command start. With multiple replicas, migrations would run as a separate deployment step so instances do not race.

**Statement pagination uses offset and a separate count.** Both degrade on very long histories. Keyset pagination would be the next step.

**401 responses are not Problem Details.** The JWT bearer handler short-circuits before the exception middleware. Fixing it means handling `JwtBearerEvents.OnChallenge`.

**Health endpoints are unauthenticated.** Standard for orchestrator probes, but on a public-facing deployment they would bind to an internal port.

**`.env` is committed.** Normally it would not be. It holds local-only development values so that `docker compose up` works from a clean checkout without setup, as the brief requires. A real deployment would inject secrets from a secret manager.

---

## Dependency notes

- **FluentAssertions is pinned to 7.x.** Version 8 moved to a licence requiring payment for commercial use.
- **MediatR is pinned to 12.x.** Version 13 moved to a commercial licence.
- **NSubstitute rather than Moq,** following Moq 4.20's SponsorLink episode.

---

## Project layout

```
src/
  NovaWallet.Domain/           Money, Wallet, LedgerEntry, AuditLogEntry, domain exceptions
  NovaWallet.Application/      Commands, queries, DTOs, validators, contracts
  NovaWallet.Infrastructure/   DbContext, migrations, TransferExecutor, IdempotencyStore, JWT
  NovaWallet.Api/              Controllers, middleware, rate limiting, health checks, Dockerfile
tests/
  NovaWallet.Domain.Tests/
  NovaWallet.Application.Tests/
  NovaWallet.IntegrationTests/
```
