# AI Usage

I have tried to be precise about who caught what. Some errors I found by running the code. Some the AI flagged about its own output, which I then verified.

---

## Tools

**Claude (claude.ai, chat)** was the primary tool, used as a pair-programmer throughout: breaking the brief into a staged build plan, generating each layer, explaining design trade-offs, and diagnosing errors from pasted output.

I did not use AI to run anything. Every build, test run, migration, Docker command and manual API check was executed by me, and the results fed back into the conversation.

---

## How I worked

Rather than asking for the whole service at once, I had the AI produce a thirteen-stage plan — Domain, then Application contracts, then use cases, persistence, the transfer executor, idempotency, auth, API, middleware, health checks, tests, Docker — and built one stage at a time. After each stage I built and tested before moving on.

The point was to keep each change small enough that I could read all of it.

---

## Three concrete prompts

### 1. Planning from the brief

> *Ingest this [the brief PDF] and give me the best breakdown of prompts to build this system. Clean Architecture — API, Infrastructure, Domain, etc. Cover everything in the document: README, AI_USAGE, middleware, JWT, audit, global error handling, rate limiting on the transfer endpoint. Serilog for logs. .NET 8. Give me steps so I can build it at intervals and understand everything being done.*

**What came back:** a staged build plan with a "locked architecture" section fixing the non-negotiables up front — integer kobo, `SELECT … FOR UPDATE` with fixed lock ordering, insert-first idempotency, WAT day boundaries, append-only audit — and a review checklist per stage. Fixing those decisions at the start meant later stages didn't relitigate them.

### 2. Challenging a design decision

> *Before we start, just curious, why is money long and not decimal. Let's discuss.*

**What came back:** a useful answer that did not simply defend the choice. It conceded that `decimal` has no binary rounding error and is defensible, then argued that integer minor units are *stricter* — no sub-kobo value can exist, integer division forces remainder handling into the open, and `bigint` has no scale to mismatch. It also identified where `decimal` genuinely wins (FX rates, interest) and how to handle that: compute in `decimal`, round to kobo once at the boundary with an explicit policy.

That shaped the `Money` value object, where `ToNaira()` is the only `decimal` in the codebase and is labelled display-only.

### 3. Diagnosing a failure

> *dotnet test, 1 failed — Expected response.SourceBalanceAfterKobo to be 90000000L, but found 900000L*

**What came back:** a correct diagnosis of an error the AI itself had introduced. See the first entry below.

---

## Where the AI was wrong

### 1. A test expectation off by a factor of 100 — caught by the test run

The generated handler test funded the wallet using `Money.FromNaira(9_000)` — ₦9,000, or 900,000 kobo — then asserted the result against the literal `900_000_00`.

The intent was "₦900,000 in kobo". But C# ignores digit separators, so `900_000_00` is simply 90,000,000. The grouping made the number *look* like "nine hundred thousand naira and zero kobo", and it read plausibly on review. The setup and the assertion disagreed by exactly 100×.

**How it was caught:** the test failed when I ran it.

**The fix, and the lesson:** expectations are now expressed through the same factory as the setup — `Money.FromNaira(9_000).Kobo` — so the conversion cannot drift between the two. In a kobo-denominated system, the unit ambiguity that the type system removes from production code moves into your test *literals*, where nothing checks it. Raw monetary literals in tests are a trap.

### 2. A concurrency test that could not have tested concurrency — caught by the test run

The generated concurrency test was meant to fire 50 simultaneous transfers from one customer's wallet. Two helpers had been left as stubs:

- the customer id used to authenticate the 50 requests was the literal `"placeholder"`, so none of them owned the wallet;
- the helper for the opposing-transfers test returned **the same client** twenty times rather than twenty independent ones.

The first failed loudly: zero of fifty transfers succeeded. The second was worse, because **it passed**. The deadlock test was green while sending forty requests down what was effectively two connections — far weaker contention than it claimed.

**How it was caught:** the first by the test result. The second I would not have found by running the suite, because it was green; it came to light when the stubs were replaced.

**The fix:** both tests now authenticate independent clients as the correct customer.

**The lesson — and what I did about it:** a passing concurrency test is not evidence of anything by itself. So I verified the test could fail. I removed `FOR UPDATE` from `TransferExecutor.LockWalletAsync` and reran it: it failed. I restored the lock: it passed. I then ran it ten consecutive times to check for flakiness. That before-and-after is the actual evidence that the concurrency guarantee holds, not the green tick.

### 3. Documentation asserting a safeguard that was never built — caught by the AI while writing the README

Code comments generated early in the build stated that the append-only audit trail was "reinforced by database grants in the migration". No grants were ever written.

The claim was also subtly wrong in principle: the application connects as the table owner, and revoking an owner's own privileges does not meaningfully stop it. Doing it properly needs separate migration and runtime roles.

**How it was caught:** the AI flagged it when cross-checking the README's claims against the implementation. I did not catch it.

**The fix:** a migration adding triggers that reject `UPDATE`, `DELETE` and `TRUNCATE` on `audit_logs` regardless of the connecting role, verified by attempting a `DELETE` against the running database. The comments were corrected to describe what actually exists.

**The lesson:** generated comments and documentation are claims, and they need checking against the code as rigorously as the code does. In a regulated domain, a comment asserting a control that isn't there is worse than no comment — it is what an auditor would read.

### 4. An abstraction that was structurally impossible — caught during implementation

The Application layer contracts included an `IAuditLogWriter` interface with a single `AppendAsync` method, for handlers to call after money moved.

That cannot work. The audit row must commit in the *same transaction* as the balance change, or a crash between the two leaves money moved with no audit record. A separately injected writer either opens its own transaction — breaking atomicity — or has the executor's transaction passed into it, which is a leaky abstraction.

**How it was caught:** when the transfer executor was implemented and the transaction boundary became concrete, the audit writes had to go directly into it, and the interface was left with no valid caller.

**The fix:** the interface was deleted. An interface that nothing can correctly use suggests a design that isn't there.

**The lesson:** transactional boundaries can't be designed from the interface layer alone. The contract looked clean on paper and was only shown to be wrong by implementing it.

### 5. A rate limit that collapsed under parallel tests — caught by the test run

The token-issuance endpoint is rate-limited per IP address. Each test authenticates at least twice, xUnit runs test classes in parallel, and every request from the in-memory test server shares one address. Ten of eleven tests failed with 429.

**How it was caught:** the test run.

**The fix:** rate limits became configurable, with looser values under the Testing environment. Production defaults are unchanged.

**The lesson:** this was the limiter working correctly, but it exposed a real property of the design. IP-based partitioning collapses whenever many logical clients share an origin — and real customers behind a mobile carrier's NAT are in exactly that position. It is why transfers are limited per customer rather than per IP, and it is recorded as a trade-off in the README.

---

## Risks the AI flagged in its own output

These were identified by the AI at the time it generated the code, rather than found by me. I verified each one, but I list them separately because I didn't catch them.

- **Releasing the idempotency key on any failure.** The first transfer handler released the key whenever an exception was thrown. If the failure happened *after* the transfer committed, a retry with the same key would move the money a second time. It was narrowed so that only failures which provably occur before money moves release the key; anything indeterminate leaves the key claimed. A stuck key is recoverable; a double debit is not.

- **Validators that would never run.** Validators were registered against the request DTOs, but the MediatR pipeline resolves validators for the *command* type. As written, validation would have been silently skipped. Command-level validators were added.

- **Retry policy conflicting with manual transactions.** EF Core's `EnableRetryOnFailure` throws when a transaction is opened manually. It was removed, deliberately, so that a failure on the money path surfaces rather than being retried over a partially applied transfer.

