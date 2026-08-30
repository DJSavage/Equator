namespace Kelvinvale.Core.Persistence;

/// <summary>
/// Fixed identifiers for the seeded advisers, customers and products so the README examples, the
/// <c>.http</c> file and the tests can all refer to the same records.
/// </summary>
public static class SeedIds
{
    // Adviser 1 matches the example caller id in the brief.
    public static readonly Guid Adviser1 = Guid.Parse("3f9c1b4e-7d21-4a55-9b02-1c6e8ad47f10");
    public static readonly Guid Adviser2 = Guid.Parse("a1d2c3b4-5e6f-4a7b-8c9d-0e1f2a3b4c5d");

    public static readonly Guid Customer1 = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public static readonly Guid Customer2 = Guid.Parse("22222222-2222-4222-8222-222222222222");
    public static readonly Guid Customer3 = Guid.Parse("33333333-3333-4333-8333-333333333333");
    public static readonly Guid Customer4Minor = Guid.Parse("44444444-4444-4444-8444-444444444444");

    public static readonly Guid Customer1Isa = Guid.Parse("a5a5a5a1-0000-4000-8000-000000000001");
    public static readonly Guid Customer1Gia = Guid.Parse("a5a5a5a1-0000-4000-8000-000000000002");
    public static readonly Guid Customer2Sipp = Guid.Parse("a5a5a5a2-0000-4000-8000-000000000001");
    public static readonly Guid Customer3Isa = Guid.Parse("a5a5a5a3-0000-4000-8000-000000000001");
}
