using Kelvinvale.Core.Domain;

namespace Kelvinvale.Core.Persistence;

/// <summary>
/// Seeds a handful of advisers, customers and products. Deterministic: dates are derived from the
/// injected <see cref="TimeProvider"/> so the seeded ISAs always sit in the current tax year and
/// the minor customer is always 16.
/// </summary>
public static class DbSeeder
{
    public static void Seed(KelvinvaleDbContext db, TimeProvider clock)
    {
        if (db.Advisers.Any())
        {
            return;
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var now = clock.GetUtcNow();
        var taxYear = TaxYear.Containing(today);

        var adviser1 = new Adviser { Id = SeedIds.Adviser1, Name = "Aisha Khan" };
        var adviser2 = new Adviser { Id = SeedIds.Adviser2, Name = "Tom Reed" };

        var customer1 = new Customer
        {
            Id = SeedIds.Customer1,
            AdviserId = adviser1.Id,
            FirstName = "Grace",
            LastName = "Okafor",
            DateOfBirth = new DateOnly(1985, 3, 12),
            Email = "grace.okafor@example.com",
            Address = "12 Rowan Way, Bristol, BS1 4TT",
        };

        var customer2 = new Customer
        {
            Id = SeedIds.Customer2,
            AdviserId = adviser1.Id,
            FirstName = "Daniel",
            LastName = "Bright",
            DateOfBirth = new DateOnly(1979, 11, 1),
            Email = "daniel.bright@example.com",
            Address = "4 Kiln Cottages, Leeds, LS6 2AB",
        };

        var customer3 = new Customer
        {
            Id = SeedIds.Customer3,
            AdviserId = adviser2.Id,
            FirstName = "Priya",
            LastName = "Nair",
            DateOfBirth = new DateOnly(1990, 6, 6),
            Email = "priya.nair@example.com",
            Address = "88 Marsh Lane, Manchester, M4 5PQ",
        };

        var customer4 = new Customer
        {
            Id = SeedIds.Customer4Minor,
            AdviserId = adviser2.Id,
            FirstName = "Sam",
            LastName = "Ellis",
            DateOfBirth = today.AddYears(-16),
            Email = "sam.ellis@example.com",
            Address = "3 Beech Close, York, YO1 7HH",
        };

        var customer1Isa = new Product
        {
            Id = SeedIds.Customer1Isa,
            CustomerId = customer1.Id,
            Type = ProductType.Isa,
            OpenedAtUtc = now.AddDays(-40),
            TaxYear = taxYear,
        };
        customer1Isa.Holdings.Add(new Holding { Id = Guid.NewGuid(), ProductId = customer1Isa.Id, FundCode = "GLB-EQ-ACC", ValuePence = 500_000 });
        customer1Isa.Instructions.Add(new Instruction
        {
            Id = Guid.NewGuid(),
            ProductId = customer1Isa.Id,
            Type = InstructionType.Subscription,
            AmountPence = 500_000, // £5,000 already subscribed this tax year
            FundCode = "GLB-EQ-ACC",
            ClientReference = $"seed-{taxYear.StartYear}-c1-isa-0001",
            Status = InstructionStatus.Settled,
            CreatedAtUtc = now.AddDays(-40),
            ValueDate = today.AddDays(-38),
            SettledAtUtc = now.AddDays(-38),
            CreatedByCallerId = customer1.Id,
            TaxYear = taxYear,
        });

        var customer1Gia = new Product
        {
            Id = SeedIds.Customer1Gia,
            CustomerId = customer1.Id,
            Type = ProductType.Gia,
            OpenedAtUtc = now.AddDays(-40),
        };
        customer1Gia.Holdings.Add(new Holding { Id = Guid.NewGuid(), ProductId = customer1Gia.Id, FundCode = "UK-GILT-INC", ValuePence = 1_250_000 });

        var customer2Sipp = new Product
        {
            Id = SeedIds.Customer2Sipp,
            CustomerId = customer2.Id,
            Type = ProductType.Sipp,
            OpenedAtUtc = now.AddDays(-200),
        };
        customer2Sipp.Holdings.Add(new Holding { Id = Guid.NewGuid(), ProductId = customer2Sipp.Id, FundCode = "GLB-EQ-ACC", ValuePence = 8_400_000 });

        var customer3Isa = new Product
        {
            Id = SeedIds.Customer3Isa,
            CustomerId = customer3.Id,
            Type = ProductType.Isa,
            OpenedAtUtc = now.AddDays(-10),
            TaxYear = taxYear,
        };
        customer3Isa.Holdings.Add(new Holding { Id = Guid.NewGuid(), ProductId = customer3Isa.Id, FundCode = "GLB-EQ-ACC", ValuePence = 1_950_000 });
        customer3Isa.Instructions.Add(new Instruction
        {
            Id = Guid.NewGuid(),
            ProductId = customer3Isa.Id,
            Type = InstructionType.Subscription,
            AmountPence = 1_950_000, // £19,500 of a £20,000 allowance already used
            FundCode = "GLB-EQ-ACC",
            ClientReference = $"seed-{taxYear.StartYear}-c3-isa-0001",
            Status = InstructionStatus.Settled,
            CreatedAtUtc = now.AddDays(-10),
            ValueDate = today.AddDays(-8),
            SettledAtUtc = now.AddDays(-8),
            CreatedByCallerId = customer3.Id,
            TaxYear = taxYear,
        });

        db.Advisers.AddRange(adviser1, adviser2);
        db.Customers.AddRange(customer1, customer2, customer3, customer4);
        db.Products.AddRange(customer1Isa, customer1Gia, customer2Sipp, customer3Isa);
        db.SaveChanges();
    }
}
