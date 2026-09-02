# Seed data

## Advisers

| Ref                | Id                                     | Name       |
| ------------------ | -------------------------------------- | ---------- |
| `SeedIds.Adviser1` | `3f9c1b4e-7d21-4a55-9b02-1c6e8ad47f10` | Aisha Khan |
| `SeedIds.Adviser2` | `a1d2c3b4-5e6f-4a7b-8c9d-0e1f2a3b4c5d` | Tom Reed   |

## Customers

| Ref                      | Id                                     | Name          | Date of birth    | Adviser               | Email                     | Address                           |
| ------------------------ | -------------------------------------- | ------------- | ---------------- | --------------------- | ------------------------- | --------------------------------- |
| `SeedIds.Customer1`      | `11111111-1111-4111-8111-111111111111` | Grace Okafor  | 1985-03-12       | Adviser1 (Aisha Khan) | grace.okafor@example.com  | 12 Rowan Way, Bristol, BS1 4TT    |
| `SeedIds.Customer2`      | `22222222-2222-4222-8222-222222222222` | Daniel Bright | 1979-11-01       | Adviser1 (Aisha Khan) | daniel.bright@example.com | 4 Kiln Cottages, Leeds, LS6 2AB   |
| `SeedIds.Customer3`      | `33333333-3333-4333-8333-333333333333` | Priya Nair    | 1990-06-06       | Adviser2 (Tom Reed)   | priya.nair@example.com    | 88 Marsh Lane, Manchester, M4 5PQ |
| `SeedIds.Customer4Minor` | `44444444-4444-4444-8444-444444444444` | Sam Ellis     | today − 16 years | Adviser2 (Tom Reed)   | sam.ellis@example.com     | 3 Beech Close, York, YO1 7HH      |

Customer 4 holds no products — the minor used to exercise the SIPP `sipp.minimum-age` refusal.

## Products

| Ref                     | Id                                     | Customer           | Type | Opened         | Tax year                |
| ----------------------- | -------------------------------------- | ------------------ | ---- | -------------- | ----------------------- |
| `SeedIds.Customer1Isa`  | `a5a5a5a1-0000-4000-8000-000000000001` | Customer1 (Grace)  | ISA  | now − 40 days  | current                 |
| `SeedIds.Customer1Gia`  | `a5a5a5a1-0000-4000-8000-000000000002` | Customer1 (Grace)  | GIA  | now − 40 days  | — (GIA has no tax year) |
| `SeedIds.Customer2Sipp` | `a5a5a5a2-0000-4000-8000-000000000001` | Customer2 (Daniel) | SIPP | now − 200 days | —                       |
| `SeedIds.Customer3Isa`  | `a5a5a5a3-0000-4000-8000-000000000001` | Customer3 (Priya)  | ISA  | now − 10 days  | current                 |

Holdings by customer: Grace has an ISA + a GIA; Daniel has a SIPP; Priya has an ISA; Sam has nothing.

## Holdings

One per product. Ids are `Guid.NewGuid()` at seed time — not fixed; read them back via
`GET /api/v1/products/{productId}/holdings`.

| Product                           | Fund code     | Value (pence) | Value (£)  |
| --------------------------------- | ------------- | ------------- | ---------- |
| Customer1 ISA (`a5a5a5a1-…-001`)  | `GLB-EQ-ACC`  | 500,000       | £5,000.00  |
| Customer1 GIA (`a5a5a5a1-…-002`)  | `UK-GILT-INC` | 1,250,000     | £12,500.00 |
| Customer2 SIPP (`a5a5a5a2-…-001`) | `GLB-EQ-ACC`  | 8,400,000     | £84,000.00 |
| Customer3 ISA (`a5a5a5a3-…-001`)  | `GLB-EQ-ACC`  | 1,950,000     | £19,500.00 |

## Instructions

Two seeded instructions, both settled subscriptions, so the ISA allowance demos have history to
count against. Ids are `Guid.NewGuid()` at seed time.

| Product       | Type         | Amount (pence) | Amount (£) | Fund code    | Client reference               | Status  | Created       | Value date      | Settled       | Placed by         | Tax year |
| ------------- | ------------ | -------------- | ---------- | ------------ | ------------------------------ | ------- | ------------- | --------------- | ------------- | ----------------- | -------- |
| Customer1 ISA | Subscription | 500,000        | £5,000.00  | `GLB-EQ-ACC` | `seed-{startYear}-c1-isa-0001` | Settled | now − 40 days | today − 38 days | now − 38 days | Customer1 (Grace) | current  |
| Customer3 ISA | Subscription | 1,950,000      | £19,500.00 | `GLB-EQ-ACC` | `seed-{startYear}-c3-isa-0001` | Settled | now − 10 days | today − 8 days  | now − 8 days  | Customer3 (Priya) | current  |

`{startYear}` is the current tax year's start year (e.g. `2026` for 2026/27).

## What the fixtures set up

| Scenario                     | Records                                                                                                         |
| ---------------------------- | --------------------------------------------------------------------------------------------------------------- |
| Ownership 404                | Customer2 → Customer1's ISA; Adviser1 → Customer3 (Adviser2's customer)                                         |
| ISA allowance 422            | Subscription over £500 on Customer3's ISA → `isa.allowance-exceeded`, `remainingAllowancePence: 50000`          |
| One ISA per tax year 422     | Open a second ISA for Customer1 or Customer3 this tax year → `isa.one-per-tax-year`                             |
| SIPP minimum age 422         | Adviser2 opens a SIPP for Customer4 (age 16) → `sipp.minimum-age`                                               |
| GIA, no rules                | Adviser1 opens a GIA for any of their customers → accepted                                                      |
| Settlement tax-year boundary | Place a subscription on Customer1's ISA, advance the clock past 6 April, settle → re-booked to the new tax year |
