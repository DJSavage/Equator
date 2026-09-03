# Kelvinvale Wealth portal API

A .NET 10 Web API behind the adviser and customer journeys for a UK wealth manager. One API,
two audiences, overlapping records, different rights.

## Run it

From a clean checkout (requires the .NET 10 SDK):

```bash
dotnet test                               # 74 tests: auth negative paths, product rules, settlement, audit
dotnet run --project src/Kelvinvale.Api   # http://localhost:5080
```

`GET /health` is anonymous. In Development the OpenAPI doc is at `/openapi/v1.json`, with Swagger UI
at `/swagger` and Scalar at `/scalar` (both served from that one document). Persistence is in-memory
SQLite, seeded on startup (`src/Kelvinvale.Core/Persistence/SeedIds.cs`, also wired into
`Kelvinvale.Api.http`).

## Seeded identities

Authenticate by sending `X-Caller-Id` with `X-Caller-Role` set to the exact string `Adviser` or
`Customer` (case-sensitive). Each adviser sees only their own customers; each customer sees only
their own records.

The full list of seeded advisers, customers, products, holdings and instructions — with ids,
ownership and the scenarios each fixture sets up — is in [`docs/SEED_DATA.md`](docs/SEED_DATA.md).

## Authorisation model

**Identity.** A fake authentication handler (`StubAuthenticationHandler`) reads `X-Caller-Id` and
`X-Caller-Role` and emits the same `NameIdentifier` + `Role` claims a JWT would. Missing headers →
401 via the fallback policy; malformed headers → 401 explicitly. Swapping in a real IdP is a
registration change; nothing downstream moves.

**Two checks, kept separate.**

- *Role* is declarative: `[Authorize(Policy = "IsAdviser" | "IsCustomer")]` on the actions. Opening
  products is adviser-only; editing personal details and placing instructions is customer-only.
- *Ownership* is data-dependent, so it runs after the record loads. There is exactly one ownership
  question — *may this caller reach this customer's records?* — as a pure function
  (`CustomerAccess.IsAllowed`, unit-tested from the wrong account) wrapped in a resource-based
  `AuthorizationHandler<CanAccessCustomerRequirement, Customer>`. Product and instruction routes
  resolve `productId → Product → Customer` and ask the same question. Ids always come from the
  route, never the body.

**Deny by default.** A fallback `RequireAuthenticatedUser` policy covers everything under
`/api/v1`; `/health` and the docs opt out with `AllowAnonymous`.

**Trade-offs accepted.** The stub trusts headers completely — fine here, it is the seam a real IdP
slots into. A record that exists but is not yours returns **404, not 403**, so one customer cannot
probe another's holdings by status code; the cost is that a genuine "not found" and "not yours" look
alike. Ownership loads the aggregate once per request; there is no field-level authorisation beyond
allow-list DTOs.

## The domain calls the brief left open

Full reasoning and rejected alternatives are in [`docs/DECISIONS.md`](docs/DECISIONS.md).

- **Over the ISA allowance →** the whole instruction is refused (HTTP 422, `application/problem+json`,
  `code: isa.allowance-exceeded`, with `remainingAllowancePence` so the caller can resubmit a figure
  that fits). Atomic and auditable; partial fills were rejected for the reconciliation surface.
- **When is the allowance checked →** on arrival *and* re-checked at settlement. Arrival counts
  pending plus settled subscriptions, so two near-simultaneous instructions cannot both pass and then
  jointly breach. Settlement is authoritative.
- **In-flight across 6 April →** the instruction is booked to, and re-judged against, the tax year it
  **settles** in. A subscription that no longer fits the new year's allowance is rejected at
  settlement with an audit entry.
- **What a refused caller sees →** RFC 9457 problem JSON: stable `type` URI, machine `code`,
  `detail`, `traceId`, and rule-specific members. 422 for any business-rule refusal, 400 for
  malformed input, 401/403/404 for auth. Every refusal is also audited and logged at warning.

## Supportability

`IAuditWriter` stages an `AuditEntry` onto the same unit of work as the change it records, so the
trail cannot drift from the data. Every mutation — accepted or refused — is captured with actor id,
role, action, subject, customer, correlation id and a before/after snapshot for updates.
`GET /api/v1/audit?customerId=` serves it to the assigned adviser. Serilog writes structured JSON;
`CorrelationMiddleware` stamps every log line for a request with the correlation id and caller.
`/health` includes a DbContext check.

## What I'd do next given another day

- Replace `EnsureCreated` + in-memory SQLite with **EF Core migrations against Azure SQL**; move the
  seed behind a flag.
- A real **settlement engine**: per-fund dealing calendars and bank holidays instead of "T+2 working
  days", plus an idempotent outbox for the settlement pass.
- A dedicated **Compliance role** (audit is currently adviser-scoped) and audit read filters/paging.
- **Switch** instructions carry a target fund; model that and apply switches to holdings.
- Flexible-ISA withdrawal-and-replace rules; a configurable allowance/rules table rather than
  constants; SIPP minimum pension age from configuration (55 today, 57 from 2028).
- Concurrency test for two racing subscriptions under a real transaction; rate limiting; contract
  tests for the problem-response shape.

## Hosting and deployment on Azure

- **Host:** Azure App Service (Linux, .NET 10) — or Container Apps for scale-to-zero. Config from
  App Configuration + Key Vault via **managed identity**; no secrets in the repo.
- **Data:** Azure SQL via EF Core migrations. Audit rows in the same database and shipped to Log
  Analytics for retention.
- **Observability:** Application Insights + a Serilog sink; `/health` wired to the App Service health
  probe and a post-deploy smoke test.
- **Infra:** Bicep.
- **On every pull request** (GitHub Actions): `dotnet restore` → `dotnet build` with
  warnings-as-errors → `dotnet test` with a coverage gate → `dotnet format --verify-no-changes` →
  `dotnet list package --vulnerable --include-transitive`. Merge to `main` deploys to a staging
  slot, smoke-tests `/health`, then swaps to production.

## How I used AI

Built with **Claude Code (Claude Sonnet)**. I used it to scaffold the solution, draft the entity
model, EF configuration and the controller/handler boilerplate, and to generate the first pass of
the test suite from a list of scenarios I specified. I directed the architecture (three projects,
resource-based ownership as a pure rule, the product-policy pipeline, the settlement re-check) and
the four open domain decisions myself.

Things I corrected: the model was steering toward per-endpoint role attributes with ownership baked
into each; I pulled ownership into a single resource-based rule. It initially put `[property:]`
DataAnnotations on record parameters, which .NET 10's validator rejects at runtime — removed in
favour of implicit required from nullable reference types. It also missed that SQLite cannot sort or
filter `DateTimeOffset`; fixed with a `DateTimeOffsetToBinaryConverter` convention. I can walk
through any line.
