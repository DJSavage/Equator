# Decisions

These are the decsions I made with Claude whilst in plan mode. The four calls the brief
deliberately left open are at the end.

## Structure
There were a few options for how to structure the solution:
1. A single project with one folder for `Api`, plus tests. Fastest to build, but the weakest separation of concerns.
2. 2 projects with `Api` and `Core`, and a third for tests. The domain entities, product rules etc. would sit in a testable `Core` library. 
3. 5 projects: `Api`, `Core`, `Domain`, `Infrastructure`, and `Tests`. This is the most "correct" DDD approach, but it adds ceremony and build time.

I decided to go with option 2, which is a modular-monolith style. The `Core` project references EF Core directly, rather than having a pure domain 
with an infrastructure layer behind ports. This keeps the separation of concerns clear without adding too much ceremony.

## Persistence
The brief allows for in-memory persistence, so I selected that option over a real database. 
There were 3 options for in-memory persistence:
1. Plain in-memory repositories behind interfaces - no real database at all, but fastest to build.
2. EF Core with SQLite in-memory, keeping one connection open for the process lifetime. A real relational database with real transactions and contraints.
3. EF Core's in-memory provider, which is not a relational database and does not support transactions or constraints.

I chose option 2, EF Core with SQLite in-memory. It is the only option that lets a database level contraint enforce "one ISA per customer per tax year" rather than enforcing it in code.
I used `EnsureCreated` and a seeder, without migrations, to keep things simple.

## API style
We could have had Minimal ApIs, or MVC controllers with `[ApiController]` and attribute routing. 
I chose the latter, as it is familiar to reviewers and allows for clear authorization checks.

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

