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

Every request answers three questions, in order. They are kept separate on purpose: each has a
different answer and a different failure code.

**1. Who are you?** 

There is no real login in this build.

`StubAuthenticationHandler` reads two headers - `X-Caller-Id` (a GUID) and `X-Caller-Role` 
(`Adviser` or `Customer`), and turns them
into the same claims a real JWT would carry (Api.Auth.StubAuthenticationHandler). 

A missing header gives a 401; a malformed one (not a
GUID, or a role that isn't one of the two) also gives a 401, with a reason. Everything downstream
reads claims, never headers, so swapping in a real identity provider should just be a registration change.

**2. Is the role allowed here?** Some actions belong to one role only: opening a product is for
advisers; editing personal details and placing instructions are for customers. That is declared on
the endpoint with `[Authorize(Policy = "IsAdviser")]` / `"IsCustomer"`, and the wrong role gets a
**403** - the door is closed to that role regardless of whose data is behind it. Endpoints both
roles share (viewing a customer, listing their products) carry no role attribute; for those,
ownership is the only gate.

**3. Ownership. Do these records belong to you?** 

- **What "belongs to you" means.** This is enforced by a plain function at
  (`Core.Auth > CustomerAccess.IsAllowed`): a customer may reach a customer record if it is their own
  (`callerId == customer.Id`); an adviser may reach it if they are the adviser named on that
  customer's file (`callerId == customer.AdviserId`). "Which adviser looks after this customer" is
  not a claim on the token and not a separate assignment table - it is an `AdviserId` column on the
  customer row, set when the adviser creates the customer. One foreign key, and the check is a field
  comparison.
- **How a request for someone else's data is refused.** The check always runs against the *loaded
  record*, never against an id taken from the URL or the request body. The controller fetches the
  customer the route points at, then hands that object to the check, which compares the caller (X-Caller-Id)
  against the record's own `Id` / `AdviserId`. So a customer calling
  `GET /customers/{another-id}/products` is not refused because two strings differ - it is refused
  because the customer object that id resolves to is not them. A `customerId` in a request body is ignored
  entirely; the route is only ever a lookup key.
- **Product and instruction routes.** Those URLs carry no customer id (`/products/{productId}/…`).
  The controller loads the product, walks to its customer, and asks the same single question. There
  is no second "can I see this product" concept to keep in sync.
- **Why a handler and not middleware.** Ownership depends on data that has to be fetched first, so
  it cannot run in middleware (app.UseAuthentication() / app.UseAuthorization() etc) ahead of the record load. The controller calls (`CanAccessCustomerHandler`) 
  right after the load. The rule inside it has no database or framework, so it can be unit-tested directly against the case that matters: a caller who isn't the owner being turned away'.

**Authorisation Trade-offs accepted.**

- **A record that exists but isn't yours returns 404, not 403.** By returning a 403, we also confirm that the record
  exists, letting one customer probe for another's products by trying ids. The trade-off here is that a
  genuine typo and a real permission problem look alike in responses and logs.
- **The stub trusts its headers completely.** StubAuthenticationHandler checks only that X-Caller-Id and X-Caller-Role are 
present and well-formed — it does not verify them, so a caller can claim any identity or role by setting two headers. 
As it stands the API has an authorisation model but no authentication: on an open endpoint anyone could impersonate any 
customer or adviser, and the ownership checks would pass because the system believes the caller is who they say. 
his was a deliberate choice: the brief specifies identity and role arriving as stubbed headers in place of claims from a 
real provider, and puts the weight of the task on the authorisation model rather than on authentication — a separate, 
well-solved problem. Keeping the stub also lets the API and its whole test suite run from a clean checkout with no 
identity provider to stand up. Running it in production instead depends on an authenticating gateway or equivalent in 
front of the API — something this codebase doesn't include — that rejects client-supplied X-Caller-* headers and sets them 
only after validating a real credential. The containment is that headers are read in exactly one class; everything downstream 
reads claims, so replacing the stub with real JWT bearer validation is a single registration change in Program.cs and 
nothing else in the solution moves.
- **The record is loaded once per request and reused.** There is no field-level authorisation
  beyond narrow response DTOs; anyone who can see3 a record sees all of it.

## The domain calls the brief left open

Full reasoning and rejected alternatives are in [`docs/DECISIONS.md`](docs/DECISIONS.md).

- **Over the ISA allowance →** the whole instruction is refused (HTTP 422, `application/problem+json`,
  `code: isa.allowance-exceeded`, with `remainingAllowancePence` so the caller can resubmit a figure
  that fits). Atomic and auditable; partial fills were rejected for the reconciliation surface.
- **When is the allowance checked →** on arrival *and* re-checked at settlement. Arrival counts
  pending plus settled subscriptions, so two near-simultaneous instructions cannot both pass and then
  jointly breach. Settlement is authoritative.
- **In-flight across 6 April →** the instruction is booked to, and re-judged against, the tax year it
  **settles** in. A subscription that no longer fitletss the new year's allowance is rejected at
  settlement with an audit entry. If a subscription is booked on 5th April 2026 and the settlement time is T+2 working days, 
  it will settle on 7th April 2026 and be judged against the 2026/27 allowance.
- **What a refused caller sees →** RFC 9457 problem JSON: stable `type` URI, machine `code`,
  `detail`, `traceId`, and rule-specific members. 422 for any business-rule refusal, 400 for
  malformed input, 401/403/404 for auth. Every refusal is also audited and logged at warning. 
  Implemented in DomainExceptionHandler and registration of ASP.NET Core’s built‑in Problem Details service.

## Supportability

`IAuditWriter` stages an `AuditEntry` onto the same unit of work as the change it records, so the
trail cannot drift from the data. Every mutation - accepted or refused - is captured with actor id,
role, action, subject, customer, correlation id and a before/after snapshot for updates.
`GET /api/v1/audit?customerId=` serves it to the assigned adviser. Serilog writes structured JSON;
`CorrelationMiddleware` stamps every log line for a request with the correlation id and caller.
`/health` includes a DbContext check.

## What I'd do next given another day

- **Move persistence to SQL Server with EF Core migrations.** Replace
`Database.EnsureCreated()` and the process-lifetime in-memory SQLite connection with a migrated
schema against SQL Server (Azure SQL when deployed, a local or containerised instance otherwise).
- **Add paging and filtering to Audit** The Audit endpoint returns every matching record in one payload.
Add paging and allow it to be filtered on outcome (Refused etc).
- **Add a test for the problem-response shape** Create a test to ensure the problem-response JSON is the expected 
shape and that a refactor hasn't silently broken the reporting.

## Hosting and deployment on Azure

The API will run on Azure App Service and the code would live in an Azure Dev Ops repo.
The automation that builds and ships it is Azure Pipelines.
The Azure side would be set up once - either through the portal or some CLI commands.

**2 Scenarios: #1 the app as is, or #2 the app with an Azure SQL database**

**Scenario #1** Not ideal. The app keeps its entire database in memory and rebuilds it from the seed data every time it starts.
This is okay for this small demo environment, but nothing else. When a change is merged to the main branch, the pipeline
builds the app, runs the tests, and pushes it to to App Service. The app would restart and reseed and should call the
/health url to confirm it is working.

**Scenario #2** The app is backed by a real Azure SQL database and so the data survives restarts. The app can run multiple
instances and scale if it needs to. On a merge to main branch, 2 things would need to happen: the database schema has to be brought up to date, 
and then the new code goes live. The app would deploy to a staging slot for a final pipeline test. If the test succeeds, the the 
live and staging slots are swapped over.

**What should happen on a pull request**
A developer initiates a pull request by finishing their work on their own copy of the code, pushing it up to Azure DevOps, 
and asking for it to be folded into the main version of the project along with a short note on what changed and why. 
That request automatically sets off the project's build-and-check routine: a clean machine takes the main version with 
the proposed change layered on top, downloads the outside libraries, and compiles everything to a standard where even a 
minor compiler issue counts as a failure. It then runs the whole test suite, checks the code is formatted the way the project 
expects, and scans the dependencies against a public list of known security problems - and if there's a real database in the picture, 
it also checks that any change to the shape of the stored data came with the instructions to bring the live database into line. 
While that runs, at least one other developer reads the change by hand and can ask for adjustments. All of it shows up as a checklist 
on the pull request page, and until every item is green — build, tests, formatting, security scan, and a reviewer's approval - 
the merge button stays switched off, deliberately, so nobody can force a change through with a failing check or no review. 
If something comes back red, the developer pushes a fix and the routine runs again. Once everything passes and the change is merged, 
the pull request is done — nothing has been deployed yet; sending it to Azure is a separate step that happens afterwards, when the 
same routine runs once more on the finished main version and then pushes it out.

## How I used AI

I built the project with **Claude Code CLI (Claude Sonnet)**. 

I had not worked with regulated financial service platforms before, and I wanted to get the scaffolding right and the domain model correct at the start.

I was certain that Claude had seen enough financial service code to be able to generate a good starting point, and I wanted to avoid the risk of building 
the wrong model and having to refactor.

We started out in **plan** mode, where I showed Claude the project brief.

We then had a discussion where Claude asked me some questions about the project and I made some decisions about the domain model and the 
architecture, such as ISA over-allowance handling: I decided to refuse the entire instruction. A full list of decisions I made with Claude during 
the plan stage is in [`docs/DECISIONS.md`](docs/DECISIONS.md).

Claude made a couple of suggestions that I rejected, such as adding an inline ownership check to every endpoint. 
I rejected that because it would have been a maintenance burden and would have risked inconsistencies. 
I wanted a single ownership rule that could be unit-tested and reused.

I asked to Claude to review the decisions ahead of progressing any further and he flagged up a problem with SQLite having no native
DateTime type. EF Core SQLite provider maps DateTime to a text string. We will use EF Core DateTimeOffsetToBinaryConverter to convert the date to 
sigle 8-byte long value. The stored column is now an opaque integer - not readable if you open the .sqlite file directly, and not portable to another 
engine without the same converter. I accepted this as part of the trade-offs on selecting SQLite in the first place.

I then asked Claude to scaffold the architecture according to the project structure I had decided on, and to generate the first pass of the code.

I also then asked Claude to add Swagger to the solution, as that is how I like to work with APIs. 
I then asked Claude to add an 'Authorise' button to Swagger, so I can easily add authorised Adviser or Customer in order to test the endpoints manually.

I then asked Claude to add a test suite, and to generate the first pass of the tests.

After which, I built the solution and checked for compile errors, and then ran the solution from visual studio so that I could trial endpoints in 
Swagger and trace the flow through the code with the debugger to make sure all was working as agreed.
