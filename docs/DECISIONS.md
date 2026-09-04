# Decisions

Every non-obvious choice, why it was made, and the trade-off accepted. The four calls the brief
deliberately left open are at the end.

## Structure and tooling

| # | Decision | Why | Trade-off |
|---|----------|-----|-----------|
| D1 | Three projects: `Kelvinvale.Api`, `Kelvinvale.Core`, `Kelvinvale.Tests` | Domain, EF model, product rules and application handlers sit in a testable `Core`; controllers, auth wiring and the host in `Api`. Clean separation without five-project ceremony in a four-hour build. | `Core` references EF Core (modular-monolith style) rather than a pure domain with an infrastructure layer behind ports. |
| D2 | EF Core + SQLite, one in-memory connection kept open for the process lifetime; `EnsureCreated` + seeder, no migrations | Real transactions and a **partial unique index** enforce "one ISA per customer per tax year" at the database, not only in a service. Zero external infra. | No migration history - the first "next day" item. SQLite serialises writes, so cross-instance concurrency is not exercised. |
| D3 | MVC controllers, `[ApiController]`, attribute routing under `api/v1` | Familiar to reviewers; `[Authorize(Policy=…)]` for role and an `IAuthorizationService` call for ownership read clearly. | More ceremony than minimal APIs; kept down by thin controllers that delegate to `Core` handlers. |
| D4 | No repository abstraction; handlers use `KelvinvaleDbContext` directly | Integration tests run against real in-memory SQLite, which is fast enough that a mock-repository seam would add indirection and no value. | Handlers are coupled to EF Core. Acceptable at this size. |
| D5 | `TimeProvider` everywhere for the clock; `FakeTimeProvider` in tests | Deterministic tax-year-boundary tests; no ambient `DateTime.UtcNow`. | - |
| D6 | Serilog, console JSON (`RenderedCompactJsonFormatter`) | Structured logs; `CorrelationMiddleware` puts correlation id + caller onto every line via `LogContext`. | - |
| D7 | xUnit + `WebApplicationFactory`; plain `Assert` (no FluentAssertions) | Standard; avoids the FluentAssertions licence change. | Slightly more verbose assertions. |
| D8 | Audit staged onto the same `DbContext` unit of work as the change | The trail commits or rolls back with the data - it cannot drift. | The audit writer shares the request's `DbContext` lifetime (scoped). |

## Authorisation

| # | Decision | Why | Trade-off |
|---|----------|-----|-----------|
| A1 | Fake `AuthenticationHandler` reading `X-Caller-Id` / `X-Caller-Role`, emitting `NameIdentifier` + `Role` claims, registered as the default scheme | The brief allows it and says production values arrive as claims. The rest of the app only sees claims, so the real-IdP swap is DI-only. | Trusts headers entirely. |
| A2 | One ownership concept - *can this caller reach this customer's records* - as a pure function (`CustomerAccess.IsAllowed`) wrapped in a resource-based `AuthorizationHandler<_, Customer>` | Ownership is data-dependent and must run after the record loads. The pure function is unit-tested directly from the wrong account, which is where the interview pushes. Product/instruction routes resolve to a `Customer` and ask the same question - no second concept. | The aggregate is loaded in the controller and handed to the check; a filter-based approach would centralise the "load then check" but obscure which query ran. |
| A3 | Deny by default: fallback `RequireAuthenticatedUser` policy over everything, `AllowAnonymous` only on `/health` and the dev docs | A new endpoint is closed until someone opens it. | - |
| A4 | **404, not 403**, when a record exists but is not the caller's; 403 only for a wrong *role*; 401 unauthenticated | A customer must not be able to detect another customer's ISA by status code. | A real "not found" and "not yours" are indistinguishable to the caller and in logs. |
| A5 | Route ids win; a `customerId` in a request body is ignored | Removes a whole class of confused-deputy bugs. | - |
| A6 | Personal-detail edits are customer-only; opening products is adviser-only; placing instructions is customer-only (an instruction is "a customer's request") | Straight reading of the two journeys in the brief. | An adviser cannot correct a customer's details or instruct on their behalf. Stated as an assumption. |
| A7 | Audit read (`GET /api/v1/audit`) is restricted to the assigned adviser | The brief defines only Adviser and Customer roles. | A real system has a Compliance role; adviser stands in for it here. |

## Products and instructions

| # | Decision | Why | Trade-off |
|---|----------|-----|-----------|
| P1 | `IProductPolicy` per type (`EnsureCanOpen` / `EnsureCanInstruct` / `EnsureCanSettle` / `OnAccepted`), discovered via `IEnumerable<IProductPolicy>` and a resolver | Adding a fourth product type is one class plus one DI line; the handlers and controllers never branch on `ProductType`. Proven by `ProductPipelineExtensibilityTests`. | An unknown product type is a 400 at the resolver rather than a compile error. |
| P2 | SIPP minimum pension age is a constant (`55`) | Keeps the four hours on the pipeline. | Legislated to rise to 57 in 2028; a production system sources this from a rules table. Flagged in code. |
| P3 | `clientReference` is an idempotency key, unique per product; a replay returns the original outcome (including re-raising a previous refusal) | The field is in the brief's payload and that is its natural use. | A replayed *rejected* reference returns 422 again rather than a fresh evaluation. |
| P4 | A refused instruction is **persisted** as a `Rejected` instruction (with `RefusalCode`) as well as audited | Compliance sees attempts, not just successes; the customer can see why in the instruction list. | Writes a row for a refused request. |
| P5 | Settlement runs as a `BackgroundService` on a 15s timer; the work is an injectable `ISettlementRunner` | Tests drive `RunDueAsync` directly after advancing the clock - deterministic, no `Task.Delay`. | Not a durable/at-least-once processor; an outbox is a "next day" item. |
| P6 | "T+2 working days" settlement calendar (weekends only, no bank holidays) | Enough to exercise the value-date and tax-year-boundary logic. | Real dealing calendars are per-fund. |
| P7 | `Switch` instructions are accepted and settled but do not move holdings | A switch needs a target fund, which the payload does not carry. | Modelled as out of scope; noted in code and README. |

## The four calls the brief left to us

### 1. An instruction that would take an ISA past the annual allowance

**Decision: refuse the whole instruction.** HTTP 422, `application/problem+json`,
`code: isa.allowance-exceeded`, with `annualAllowancePence`, `remainingAllowancePence` and
`requestedAmountPence` so the caller can immediately resubmit a figure that fits.

*Considered and rejected:* **partial fill** to the remaining headroom. It turns one instruction into
two outcomes (a booked part and a refused part), needs a requested-vs-accepted amount on the record,
and pushes reconciliation onto every downstream consumer and the client - for a modest convenience.
It is also asymmetric: withdrawals and switches have no "partial" meaning. **Accept-and-flag** was
rejected outright - you would be knowingly booking a regulatory breach. A future "cap to the
headroom" convenience could be an explicit opt-in flag on the request.

### 2. Is the allowance checked on arrival or at settlement?

**Decision: both.** On arrival the check counts subscriptions in `Received` *and* `Settled` state,
so two instructions submitted seconds apart cannot each pass against the settled total and then
jointly breach (`IsaRuleTests.Two_subscriptions_that_individually_fit_but_jointly_breach_are_caught`).
At settlement the instruction is re-evaluated and this is the authoritative check.

*Considered and rejected:* **arrival only**. Simpler, but it cannot handle other subscriptions
settling first, or the tax year rolling, between capture and settlement - and this is a money
system. The settlement lifecycle (a `Received → Settled | Rejected` state machine plus a runner)
is the cost, and it is small.

### 3. An in-flight instruction that crosses the tax-year boundary

**Decision: book it to the tax year it settles in, and re-judge it there.** An ISA subscription
captured on 2 April but settling on 8 April counts against the new tax year's allowance, checked at
settlement against that year's headroom. If it no longer fits, it is rejected at settlement with an
audit entry (`SettlementBoundaryTests`).

*Considered and rejected:* **book to the tax year it was instructed in**. It honours submission-time
intent but can retroactively breach a closed year's allowance and credits a prior-year subscription
after year end - awkward for reporting. Booking to the settlement year also falls straight out of
decision 2.

### 4. What a caller sees when a rule refuses their instruction

**Decision: RFC 9457 `application/problem+json`** with a stable `type` URI
(`https://kelvinvale.example/problems/{code}`), a machine `code`, a human `detail`, `traceId`, and
rule-specific extension members. Status codes: **422** for any well-formed request a business rule
refuses; **400** for malformed input; **401 / 403 / 404** for authentication, wrong role, and
not-yours respectively; **409** reserved for genuine version/idempotency conflicts. Every refusal is
also written to the audit trail and logged at warning with its `code`.

*Considered and rejected:* mixing **409** (for state rules like "already holds an ISA") and **403**
(for eligibility like "must be 18"). One status for all business-rule refusals keeps client handling
simple; the `code` carries the specificity.

## Assumptions

- Advisers are seeded, not self-registering. Customers do not authenticate themselves in this build
  (the stub stands in).
- A customer's date of birth is identity data and is not editable via `PUT /customers/{id}`.
- Money is integer pence throughout; no currency other than GBP.
- The allowance table currently covers tax years 2022/23–2026/27 and falls back to £20,000.
